﻿/*
 * Copyright(c) 2022 Ori @MidiBard2
 */

using System.Reflection;
using System.Runtime.InteropServices;
using Dalamud.Game;

namespace DalamudDoot.Offsets;

public static class OffsetManager
{
    public static void Setup(ISigScanner scanner)
    {
        var props = typeof(Offsets).GetProperties(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
            .Select(i => (prop: i, Attribute: i.GetCustomAttribute<SigAttribute>())).Where(i => i.Attribute != null);

        var exceptions = new List<Exception>(100);
        foreach (var (propertyInfo, sigAttribute) in props)
        {
            try
            {
                var sig = sigAttribute?.SigString;
                sig = string.Join(' ', sig?.Split(new[] { ' ' }, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                    .Select(i => i == "?" ? "??" : i)!);

                nint address = 0;
                switch (sigAttribute)
                {
                    case StaticAddressAttribute:
                        if (scanner != null) address = scanner.GetStaticAddressFromSig(sig);
                        break;
                    case FunctionAttribute:
                        if (scanner != null) address = scanner.ScanText(sig);
                        break;
                    case OffsetAttribute:
                        {
                            if (scanner != null) address = scanner.ScanText(sig);
                            address += sigAttribute.Offset;
                            var structure = Marshal.PtrToStructure(address, propertyInfo.PropertyType);
                            propertyInfo.SetValue(null, structure);
                            Api.PluginLog?.Debug($"[{nameof(OffsetManager)}][{propertyInfo.Name}] {propertyInfo.PropertyType.FullName} {structure}");
                            continue;
                        }
                    default:
                        throw new ArgumentOutOfRangeException(null);
                }

                address += sigAttribute.Offset;
                propertyInfo.SetValue(null, address);
                Api.PluginLog?.Debug($"[{nameof(OffsetManager)}][{propertyInfo.Name}] {address.ToInt64():X}");
                Api.PluginLog?.Debug($"[{nameof(OffsetManager)}][{propertyInfo.Name}] {PerformActions.MainModuleRva(address)}");
            }
            catch (Exception e)
            {
                Api.PluginLog?.Error(e, $"[{nameof(OffsetManager)}][{propertyInfo.Name}] failed to find sig : {sigAttribute?.SigString}");
                exceptions.Add(e);
            }
        }

        if (exceptions.Any())
        {
            throw new AggregateException(exceptions);
        }
    }
}

internal class SigAttribute : Attribute
{
    protected SigAttribute(string sigString, int offset = 0)
    {
        SigString = sigString;
        Offset = offset;
    }

    public readonly string SigString;
    public readonly int Offset;
}

[AttributeUsage(AttributeTargets.Property)]
internal sealed class StaticAddressAttribute : SigAttribute
{
    public StaticAddressAttribute(string sigString, int offset = 0) : base(sigString, offset) { }
}

[AttributeUsage(AttributeTargets.Property)]
internal sealed class FunctionAttribute : SigAttribute
{
    public FunctionAttribute(string sigString, int offset = 0) : base(sigString, offset) { }
}

[AttributeUsage(AttributeTargets.Property)]
internal sealed class OffsetAttribute : SigAttribute
{
    public OffsetAttribute(string sigString, int offset) : base(sigString, offset) { }
}