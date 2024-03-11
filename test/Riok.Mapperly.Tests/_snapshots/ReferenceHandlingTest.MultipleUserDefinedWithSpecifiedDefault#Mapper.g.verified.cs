﻿//HintName: Mapper.g.cs
// <auto-generated />
#nullable enable
public partial class Mapper
{
    [global::System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "0.0.1.0")]
    public partial global::B Map(global::A a)
    {
        return MapToB(a, new global::Riok.Mapperly.Abstractions.ReferenceHandling.PreserveReferenceHandler());
    }

    [global::System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "0.0.1.0")]
    private partial global::D MapD(global::C source)
    {
        return MapToD(source, new global::Riok.Mapperly.Abstractions.ReferenceHandling.PreserveReferenceHandler());
    }

    [global::System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "0.0.1.0")]
    private partial global::D MapDIgnore(global::C source)
    {
        return MapToD1(source, new global::Riok.Mapperly.Abstractions.ReferenceHandling.PreserveReferenceHandler());
    }

    [global::System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "0.0.1.0")]
    private global::B MapToB(global::A source, global::Riok.Mapperly.Abstractions.ReferenceHandling.IReferenceHandler refHandler)
    {
        if (refHandler.TryGetReference<global::A, global::B>(source, out var existingTargetReference))
            return existingTargetReference;
        var target = new global::B(MapToD1(source.Value, refHandler));
        refHandler.SetReference<global::A, global::B>(source, target);
        return target;
    }

    [global::System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "0.0.1.0")]
    private global::D MapToD(global::C source, global::Riok.Mapperly.Abstractions.ReferenceHandling.IReferenceHandler refHandler)
    {
        if (refHandler.TryGetReference<global::C, global::D>(source, out var existingTargetReference))
            return existingTargetReference;
        var target = new global::D(source.StringValue);
        refHandler.SetReference<global::C, global::D>(source, target);
        target.IntValue = source.IntValue;
        return target;
    }

    [global::System.CodeDom.Compiler.GeneratedCode("Riok.Mapperly", "0.0.1.0")]
    private global::D MapToD1(global::C source, global::Riok.Mapperly.Abstractions.ReferenceHandling.IReferenceHandler refHandler)
    {
        if (refHandler.TryGetReference<global::C, global::D>(source, out var existingTargetReference))
            return existingTargetReference;
        var target = new global::D(source.StringValue);
        refHandler.SetReference<global::C, global::D>(source, target);
        return target;
    }
}