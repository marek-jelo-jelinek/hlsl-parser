using System;
using System.Collections.Generic;

namespace HlslParser.Lexing
{
    [Flags]
    public enum HlslKeywordCategory
    {
        None = 0,
        ScalarType = 1 << 0,
        VectorType = 1 << 1,
        MatrixType = 1 << 2,
        ResourceType = 1 << 3,
        ControlFlow = 1 << 4,
        Declaration = 1 << 5,
        Modifier = 1 << 6,

        Type = ScalarType | VectorType | MatrixType | ResourceType
    }

    /// <summary>
    /// Single source of truth for the HLSL/Cg reserved-word surface: whether a given
    /// identifier-shaped lexeme is a keyword at all, its canonical (interned) spelling, and which
    /// category/categories it belongs to.
    /// </summary>
    public static class HlslKeywords
    {
        private static Dictionary<string, KeywordInfo> _table;

        private static IReadOnlyDictionary<string, KeywordInfo> Table
        {
            get
            {
                _table ??= BuildTable();
                return _table;
            }
        }

        public static bool IsKeyword(string text)
        {
            return text != null && Table.ContainsKey(text);
        }

        public static bool TryGetCanonical(string text, out string canonical, out HlslKeywordCategory category)
        {
            if (text != null && Table.TryGetValue(text, out var info))
            {
                canonical = info.CanonicalText;
                category = info.Category;
                return true;
            }

            canonical = null;
            category = HlslKeywordCategory.None;
            return false;
        }

        public static HlslKeywordCategory GetCategory(string text)
        {
            return TryGetCanonical(text, out _, out var category) ? category : HlslKeywordCategory.None;
        }

        public static bool IsTypeKeyword(string text)
        {
            return (GetCategory(text) & HlslKeywordCategory.Type) != 0;
        }

        public static bool IsModifierKeyword(string text)
        {
            return (GetCategory(text) & HlslKeywordCategory.Modifier) != 0;
        }

        public static bool IsControlFlowKeyword(string text)
        {
            return (GetCategory(text) & HlslKeywordCategory.ControlFlow) != 0;
        }

        public static bool IsResourceKeyword(string text)
        {
            return (GetCategory(text) & HlslKeywordCategory.ResourceType) != 0;
        }

        public static bool IsDeclarationKeyword(string text)
        {
            return (GetCategory(text) & HlslKeywordCategory.Declaration) != 0;
        }

        private static Dictionary<string, KeywordInfo> BuildTable()
        {
            var table = new Dictionary<string, KeywordInfo>(StringComparer.Ordinal);

            foreach (var t in new[]
                     {
                         "void", "bool", "int", "uint", "dword", "half", "float", "double",
                         "min16float", "min10float", "min16int", "min12int", "min16uint", "string"
                     })
            {
                Add(t, HlslKeywordCategory.ScalarType);
            }

            // Systematic vectorN (N=1..4) / matrixRxC (R,C=1..4) spellings - generated, not
            // handwritten, since there are ~20 spellings per base type across 6 base types.
            string[] baseTypes = { "float", "int", "uint", "bool", "half", "double" };
            foreach (var b in baseTypes)
            {
                for (var n = 1; n <= 4; n++)
                {
                    Add(b + n, HlslKeywordCategory.VectorType);
                }

                for (var r = 1; r <= 4; r++)
                {
                    for (var c = 1; c <= 4; c++)
                    {
                        Add(b + r + "x" + c, HlslKeywordCategory.MatrixType);
                    }
                }
            }

            foreach (var t in new[]
                     {
                         "Texture1D", "Texture1DArray", "Texture2D", "Texture2DArray", "Texture2DMS",
                         "Texture2DMSArray", "Texture3D", "TextureCube", "TextureCubeArray",
                         "RWTexture1D", "RWTexture1DArray", "RWTexture2D", "RWTexture2DArray", "RWTexture3D",
                         "Buffer", "RWBuffer", "StructuredBuffer", "RWStructuredBuffer",
                         "AppendStructuredBuffer", "ConsumeStructuredBuffer",
                         "ByteAddressBuffer", "RWByteAddressBuffer", "ConstantBuffer",
                         "SamplerState", "SamplerComparisonState",
                         // Cg-legacy spellings — plain superset, no dialect gating.
                         "sampler", "sampler1D", "sampler2D", "sampler3D", "samplerCUBE", "sampler_state"
                     })
                Add(t, HlslKeywordCategory.ResourceType);

            foreach (var t in new[]
                     {
                         "if", "else", "for", "while", "do", "switch", "case", "default",
                         "break", "continue", "return", "discard"
                     })
                Add(t, HlslKeywordCategory.ControlFlow);

            foreach (var t in new[] { "struct", "cbuffer", "tbuffer", "typedef" })
                Add(t, HlslKeywordCategory.Declaration);

            foreach (var t in new[]
                     {
                         "static", "const", "uniform", "extern", "shared", "groupshared", "volatile",
                         "inline", "in", "out", "inout", "precise", "row_major", "column_major",
                         "centroid", "linear", "noperspective", "nointerpolation", "sample", "noinline"
                     })
                Add(t, HlslKeywordCategory.Modifier);

            return table;

            void Add(string text, HlslKeywordCategory category)
            {
                table.Add(text, new KeywordInfo(text, category));
            }
        }

        private readonly struct KeywordInfo
        {
            public KeywordInfo(string canonicalText, HlslKeywordCategory category)
            {
                CanonicalText = canonicalText;
                Category = category;
            }

            public string CanonicalText { get; }
            public HlslKeywordCategory Category { get; }
        }
    }
}