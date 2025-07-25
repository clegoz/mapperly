using Microsoft.CodeAnalysis;
using Riok.Mapperly.Abstractions;
using Riok.Mapperly.Abstractions.ReferenceHandling;
using Riok.Mapperly.Configuration;
using Riok.Mapperly.Descriptors.Constructors;
using Riok.Mapperly.Descriptors.ExternalMappings;
using Riok.Mapperly.Descriptors.FormatProviders;
using Riok.Mapperly.Descriptors.MappingBodyBuilders;
using Riok.Mapperly.Descriptors.MappingBuilders;
using Riok.Mapperly.Descriptors.Mappings.UserMappings;
using Riok.Mapperly.Descriptors.ObjectFactories;
using Riok.Mapperly.Descriptors.UnsafeAccess;
using Riok.Mapperly.Diagnostics;
using Riok.Mapperly.Helpers;
using Riok.Mapperly.Symbols;

namespace Riok.Mapperly.Descriptors;

public class DescriptorBuilder
{
    private readonly MapperDescriptor _mapperDescriptor;
    private readonly SymbolAccessor _symbolAccessor;

    private readonly MappingCollection _mappings = new();
    private readonly InlinedExpressionMappingCollection _inlineMappings = new();

    private readonly MethodNameBuilder _methodNameBuilder = new();
    private readonly MappingBodyBuilder _mappingBodyBuilder;
    private readonly SimpleMappingBuilderContext _builderContext;
    private readonly DiagnosticCollection _diagnostics;
    private readonly UnsafeAccessorContext _unsafeAccessorContext;
    private readonly AttributeDataAccessor _attributeAccessor;

    public DescriptorBuilder(
        CompilationContext compilationContext,
        MapperDeclaration mapperDeclaration,
        SymbolAccessor symbolAccessor,
        MapperConfiguration defaultMapperConfiguration
    )
    {
        var supportedFeatures = SupportedFeatures.Build(compilationContext.Types, symbolAccessor, compilationContext.ParseLanguageVersion);
        _mapperDescriptor = new MapperDescriptor(mapperDeclaration, _methodNameBuilder, supportedFeatures);
        _symbolAccessor = symbolAccessor;
        _mappingBodyBuilder = new MappingBodyBuilder(_mappings);
        _unsafeAccessorContext = new UnsafeAccessorContext(_methodNameBuilder, symbolAccessor);
        _diagnostics = new DiagnosticCollection(mapperDeclaration.Syntax.GetLocation());
        _attributeAccessor = new AttributeDataAccessor(symbolAccessor);

        var genericTypeChecker = new GenericTypeChecker(_symbolAccessor, compilationContext.Types);
        var configurationReader = new MapperConfigurationReader(
            _attributeAccessor,
            _mappings,
            genericTypeChecker,
            _diagnostics,
            compilationContext.Types,
            mapperDeclaration.Symbol,
            defaultMapperConfiguration,
            supportedFeatures
        );

        _builderContext = new SimpleMappingBuilderContext(
            compilationContext,
            mapperDeclaration,
            configurationReader,
            _symbolAccessor,
            genericTypeChecker,
            _attributeAccessor,
            _unsafeAccessorContext,
            _diagnostics,
            new MappingBuilder(_mappings, mapperDeclaration),
            new ExistingTargetMappingBuilder(_mappings, mapperDeclaration),
            _inlineMappings,
            mapperDeclaration.Syntax.GetLocation()
        );
    }

    public (MapperDescriptor descriptor, DiagnosticCollection diagnostics) Build(CancellationToken cancellationToken)
    {
        ConfigureMemberVisibility();
        ReserveMethodNames();
        ExtractUserMappings();

        // ExtractObjectFactories needs to be called after ExtractUserMappings due to configuring mapperDescriptor.Static
        var objectFactories = ExtractObjectFactories();
        var constructorFactory = new InstanceConstructorFactory(objectFactories, _symbolAccessor, _unsafeAccessorContext);
        var formatProviders = ExtractFormatProviders();
        EnqueueUserMappings(constructorFactory, formatProviders);
        ExtractExternalMappings();
        _mappingBodyBuilder.BuildMappingBodies(cancellationToken);
        AddUserMappingDiagnostics();
        BuildMappingMethodNames();
        BuildReferenceHandlingParameters();
        AddMappingsToDescriptor();
        AddAccessorsToDescriptor();
        return (_mapperDescriptor, _diagnostics);
    }

    /// <summary>
    /// Sets the member and constructor visibility filter on the <see cref="_symbolAccessor"/> after validation.
    /// If <see cref="MemberVisibility.Accessible"/> is not set and the compilation does not have UnsafeAccessors,
    /// emit a diagnostic and update the <see cref="MemberVisibility"/> to include <see cref="MemberVisibility.Accessible"/>.
    /// </summary>
    private void ConfigureMemberVisibility()
    {
        var includedMembers = _builderContext.Configuration.Mapper.IncludedMembers;
        var includedConstructors = _builderContext.Configuration.Mapper.IncludedConstructors;

        if (_mapperDescriptor.SupportedFeatures.UnsafeAccessors)
        {
            _symbolAccessor.SetMemberVisibility(includedMembers);
            _symbolAccessor.SetConstructorVisibility(includedConstructors);
            return;
        }

        if (includedMembers.HasFlag(MemberVisibility.Accessible) && includedConstructors.HasFlag(MemberVisibility.Accessible))
        {
            return;
        }

        _diagnostics.ReportDiagnostic(DiagnosticDescriptors.UnsafeAccessorNotAvailable);
        _symbolAccessor.SetMemberVisibility(includedMembers | MemberVisibility.Accessible);
        _symbolAccessor.SetConstructorVisibility(includedConstructors | MemberVisibility.Accessible);
    }

    private void ReserveMethodNames()
    {
        foreach (var methodSymbol in _symbolAccessor.GetAllMembers(_mapperDescriptor.Symbol))
        {
            _methodNameBuilder.Reserve(methodSymbol.Name);
        }
    }

    private void ExtractUserMappings()
    {
        _mapperDescriptor.Static = _mapperDescriptor.Symbol.IsStatic;
        IMethodSymbol? firstNonStaticUserMapping = null;

        foreach (var userMapping in UserMethodMappingExtractor.ExtractUserMappings(_builderContext, _mapperDescriptor.Symbol))
        {
            // if a user defined mapping method is static, all of them need to be static to avoid confusion for mapping method resolution
            // however, user implemented mapping methods are allowed to be static in a non-static context.
            // Therefore, we are only interested in partial method definitions here.
            if (userMapping.Method is { IsStatic: true, IsPartialDefinition: true })
            {
                _mapperDescriptor.Static = true;
            }
            else if (firstNonStaticUserMapping == null && !userMapping.Method.IsStatic)
            {
                firstNonStaticUserMapping = userMapping.Method;
            }

            AddUserMapping(userMapping, false, true);
        }

        if (_mapperDescriptor.Static && firstNonStaticUserMapping is not null)
        {
            _diagnostics.ReportDiagnostic(
                DiagnosticDescriptors.MixingStaticPartialWithInstanceMethod,
                firstNonStaticUserMapping,
                _mapperDescriptor.Symbol.ToDisplayString()
            );
        }
    }

    private ObjectFactoryCollection ExtractObjectFactories()
    {
        return ObjectFactoryBuilder.ExtractObjectFactories(_builderContext, _mapperDescriptor.Symbol, _mapperDescriptor.Static);
    }

    private void EnqueueUserMappings(InstanceConstructorFactory constructorFactory, FormatProviderCollection formatProviders)
    {
        foreach (var userMapping in _mappings.UserMappings)
        {
            var ctx = new MappingBuilderContext(
                _builderContext,
                constructorFactory,
                formatProviders,
                userMapping,
                new TypeMappingKey(userMapping.SourceType, userMapping.TargetType)
            );

            _mappings.EnqueueToBuildBody(userMapping, ctx);
        }
    }

    private void ExtractExternalMappings()
    {
        foreach (var externalMapping in ExternalMappingsExtractor.ExtractExternalMappings(_builderContext, _mapperDescriptor.Symbol))
        {
            AddUserMapping(externalMapping, true, false);
        }
    }

    private FormatProviderCollection ExtractFormatProviders()
    {
        return FormatProviderBuilder.ExtractFormatProviders(_builderContext, _mapperDescriptor.Symbol, _mapperDescriptor.Static);
    }

    private void BuildMappingMethodNames()
    {
        foreach (var methodMapping in _mappings.MethodMappings)
        {
            methodMapping.SetMethodNameIfNeeded(_methodNameBuilder.Build);
        }
    }

    private void BuildReferenceHandlingParameters()
    {
        if (!_builderContext.Configuration.Mapper.UseReferenceHandling)
            return;

        foreach (var methodMapping in _mappings.MethodMappings)
        {
            methodMapping.EnableReferenceHandling(_builderContext.Types.Get<IReferenceHandler>());
        }
    }

    private void AddMappingsToDescriptor()
    {
        // add generated mappings to the mapper
        _mapperDescriptor.AddMethodMappings(_mappings.MethodMappings);
    }

    private void AddAccessorsToDescriptor()
    {
        _mapperDescriptor.UnsafeAccessors = _unsafeAccessorContext;
    }

    private void AddUserMapping(IUserMapping mapping, bool ignoreDuplicates, bool named)
    {
        var name = named ? _attributeAccessor.GetMethodName(mapping.Method) : null;
        var result = _mappings.AddUserMapping(mapping, name);
        if (!ignoreDuplicates && mapping.Default == true && result == MappingCollectionAddResult.NotAddedDuplicated)
        {
            _diagnostics.ReportDiagnostic(
                DiagnosticDescriptors.MultipleDefaultUserMappings,
                mapping.Method,
                mapping.SourceType.ToDisplayString(),
                mapping.TargetType.ToDisplayString()
            );
        }

        _inlineMappings.AddUserMapping(mapping, name);
    }

    private void AddUserMappingDiagnostics()
    {
        foreach (var mapping in _mappings.UsedDuplicatedNonDefaultNonReferencedUserMappings)
        {
            _diagnostics.ReportDiagnostic(
                DiagnosticDescriptors.MultipleUserMappingsWithoutDefault,
                mapping.Method,
                mapping.SourceType.ToDisplayString(),
                mapping.TargetType.ToDisplayString()
            );
        }
    }
}
