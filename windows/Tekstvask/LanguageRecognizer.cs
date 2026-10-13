using System.Runtime.InteropServices;

namespace Tekstvask;

/// <summary>
/// Windows' built-in language detection (Extended Linguistic Services, elscore.dll), the counterpart of Apple's
/// NaturalLanguage in the macOS app. It gives no answer for text too short to tell, such as "ok".
/// </summary>
internal static class LanguageRecognizer
{
    private static readonly Guid LanguageDetection = new("CF7E00B1-909B-4D95-A8F4-611F7C377702"); // ELS_GUID_LANGUAGE_DETECTION

    /// <summary>The text's most likely language as a code like "nb", or null when Windows can't tell.</summary>
    public static string? Recognize(string text)
    {
        try { return RecognizeOrThrow(text); }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException) { return null; } // the tests on macOS
    }

    private static string? RecognizeOrThrow(string text)
    {
        IntPtr guid = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
        try
        {
            Marshal.StructureToPtr(LanguageDetection, guid, false);
            var options = new EnumOptions { Size = (nuint)Marshal.SizeOf<EnumOptions>(), Guid = guid };
            if (MappingGetServices(ref options, out IntPtr services, out uint count) != 0 || count == 0) return null;
            try
            {
                var bag = new PropertyBag { Size = (nuint)Marshal.SizeOf<PropertyBag>() };
                if (MappingRecognizeText(services, text, (uint)text.Length, 0, IntPtr.Zero, ref bag) != 0) return null;
                try
                {
                    if (bag.RangesCount == 0) return null;
                    // The range's data is a list of language tags, most likely first: "nb\0da\0nn\0\0".
                    string? first = Marshal.PtrToStringUni(Marshal.PtrToStructure<DataRange>(bag.Ranges).Data);
                    return string.IsNullOrEmpty(first) ? null : first.Split('-')[0];
                }
                finally { MappingFreePropertyBag(ref bag); }
            }
            finally { MappingFreeServices(services); }
        }
        finally { Marshal.FreeHGlobal(guid); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct EnumOptions // MAPPING_ENUM_OPTIONS
    {
        public nuint Size;
        public IntPtr Category, InputLanguage, OutputLanguage, InputScript, OutputScript, InputContentType, OutputContentType;
        public IntPtr Guid;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PropertyBag // MAPPING_PROPERTY_BAG
    {
        public nuint Size;
        public IntPtr Ranges;
        public uint RangesCount;
        public IntPtr ServiceData;
        public uint ServiceDataSize;
        public IntPtr CallerData;
        public uint CallerDataSize;
        public IntPtr Context;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DataRange // MAPPING_DATA_RANGE
    {
        public uint StartIndex, EndIndex;
        public IntPtr Description;
        public uint DescriptionLength;
        public IntPtr Data;
        public uint DataSize;
        public IntPtr ContentType, ActionIds;
        public uint ActionsCount;
        public IntPtr ActionDisplayNames;
    }

    [DllImport("elscore.dll")]
    private static extern int MappingGetServices(ref EnumOptions options, out IntPtr services, out uint count);

    [DllImport("elscore.dll")]
    private static extern int MappingFreeServices(IntPtr services);

    [DllImport("elscore.dll", CharSet = CharSet.Unicode)]
    private static extern int MappingRecognizeText(IntPtr service, string text, uint length, uint index, IntPtr options, ref PropertyBag bag);

    [DllImport("elscore.dll")]
    private static extern int MappingFreePropertyBag(ref PropertyBag bag);
}
