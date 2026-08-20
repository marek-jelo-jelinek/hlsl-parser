using System.Collections.Generic;
using HlslParser.Lexing;
using NUnit.Framework;

namespace HlslParser.Tests.Lexing
{
    [TestFixture]
    public class HlslKeywordsTests
    {
        private static IEnumerable<string> VectorAndMatrixKeywords()
        {
            string[] baseTypes = { "float", "int", "uint", "bool", "half", "double", "fixed" };
            foreach (var b in baseTypes)
            {
                for (var n = 1; n <= 4; n++) yield return b + n;
                for (var r = 1; r <= 4; r++)
                for (var c = 1; c <= 4; c++) yield return b + r + "x" + c;
            }
        }

        [TestCaseSource(nameof(VectorAndMatrixKeywords))]
        public void SystematicVectorAndMatrixSpellingsAreTypeKeywords(string text)
        {
            Assert.IsTrue(HlslKeywords.IsKeyword(text), text + " should be a keyword");
            Assert.IsTrue(HlslKeywords.IsTypeKeyword(text), text + " should be a type keyword");
        }

        [TestCase("vector", HlslKeywordCategory.VectorType)]
        [TestCase("matrix", HlslKeywordCategory.MatrixType)]
        public void GenericVectorAndMatrixKeywordsAreRecognized(string text, HlslKeywordCategory expectedCategory)
        {
            Assert.IsTrue(HlslKeywords.IsKeyword(text));
            Assert.IsTrue(HlslKeywords.IsTypeKeyword(text));
            Assert.AreEqual(expectedCategory, HlslKeywords.GetCategory(text));
        }

        [TestCase("void")]
        [TestCase("bool")]
        [TestCase("int")]
        [TestCase("uint")]
        [TestCase("dword")]
        [TestCase("half")]
        [TestCase("float")]
        [TestCase("double")]
        [TestCase("min16float")]
        [TestCase("min10float")]
        [TestCase("min16int")]
        [TestCase("min12int")]
        [TestCase("min16uint")]
        [TestCase("string")]
        [TestCase("fixed")]
        [TestCase("RayDesc")]
        [TestCase("BuiltInTriangleIntersectionAttributes")]
        public void ScalarTypesAreTypeKeywords(string text)
        {
            Assert.IsTrue(HlslKeywords.IsTypeKeyword(text));
        }

        [TestCase("Texture2D")]
        [TestCase("Texture2DArray")]
        [TestCase("RWTexture3D")]
        [TestCase("StructuredBuffer")]
        [TestCase("RWStructuredBuffer")]
        [TestCase("AppendStructuredBuffer")]
        [TestCase("ByteAddressBuffer")]
        [TestCase("ConstantBuffer")]
        [TestCase("SamplerState")]
        [TestCase("SamplerComparisonState")]
        [TestCase("sampler2D")] // Cg legacy
        [TestCase("sampler_state")] // Cg legacy
        [TestCase("sampler2DMS")]
        [TestCase("sampler2DArray")]
        [TestCase("samplerCUBEMArray")]
        [TestCase("samplerRECT")]
        [TestCase("sampler2D_float")]
        [TestCase("sampler2D_half")]
        [TestCase("PointStream")]
        [TestCase("LineStream")]
        [TestCase("TriangleStream")]
        [TestCase("InputPatch")]
        [TestCase("OutputPatch")]
        [TestCase("RaytracingAccelerationStructure")]
        [TestCase("RayQuery")]
        [TestCase("RasterizerOrderedTexture1D")]
        [TestCase("RasterizerOrderedTexture2D")]
        [TestCase("RasterizerOrderedTexture3D")]
        [TestCase("RasterizerOrderedBuffer")]
        [TestCase("RasterizerOrderedStructuredBuffer")]
        [TestCase("RasterizerOrderedByteAddressBuffer")]
        [TestCase("SubpassInput")]
        [TestCase("SubpassInputMS")]
        [TestCase("FeedbackTexture2D")]
        [TestCase("FeedbackTexture2DArray")]
        public void ResourceTypesAreResourceKeywords(string text)
        {
            Assert.IsTrue(HlslKeywords.IsResourceKeyword(text));
            Assert.IsTrue(HlslKeywords.IsTypeKeyword(text));
        }

        [TestCase("if")]
        [TestCase("else")]
        [TestCase("for")]
        [TestCase("while")]
        [TestCase("do")]
        [TestCase("switch")]
        [TestCase("case")]
        [TestCase("default")]
        [TestCase("break")]
        [TestCase("continue")]
        [TestCase("return")]
        [TestCase("discard")]
        public void ControlFlowKeywordsAreRecognized(string text)
        {
            Assert.IsTrue(HlslKeywords.IsControlFlowKeyword(text));
        }

        [TestCase("struct")]
        [TestCase("cbuffer")]
        [TestCase("typedef")]
        public void DeclarationKeywordsAreRecognized(string text)
        {
            Assert.IsTrue(HlslKeywords.IsDeclarationKeyword(text));
        }

        [TestCase("static")]
        [TestCase("const")]
        [TestCase("uniform")]
        [TestCase("groupshared")]
        [TestCase("in")]
        [TestCase("out")]
        [TestCase("inout")]
        [TestCase("row_major")]
        [TestCase("nointerpolation")]
        [TestCase("unorm")]
        [TestCase("snorm")]
        [TestCase("globallycoherent")]
        [TestCase("point")]
        [TestCase("line")]
        [TestCase("triangle")]
        [TestCase("lineadj")]
        [TestCase("triangleadj")]
        public void ModifierKeywordsAreRecognized(string text)
        {
            Assert.IsTrue(HlslKeywords.IsModifierKeyword(text));
        }

        [TestCase("myVariable")]
        [TestCase("Float4")] // wrong case
        [TestCase("TEXTURE2D")] // wrong case
        [TestCase("_temp")]
        [TestCase("Struct")] // wrong case
        public void NonKeywordsAndWrongCaseAreNotKeywords(string text)
        {
            Assert.IsFalse(HlslKeywords.IsKeyword(text));
        }

        [Test]
        public void NullTextIsNotAKeyword()
        {
            Assert.IsFalse(HlslKeywords.IsKeyword(null));
        }

        [Test]
        public void TryGetCanonicalReturnsReferenceEqualStringsAcrossCalls()
        {
            HlslKeywords.TryGetCanonical("float4", out var first, out _);
            HlslKeywords.TryGetCanonical("float4", out var second, out _);

            Assert.AreSame(first, second);
        }

        [Test]
        public void GetCategoryReturnsNoneForNonKeyword()
        {
            Assert.AreEqual(HlslKeywordCategory.None, HlslKeywords.GetCategory("notAKeyword"));
        }
    }
}
