using System;
using System.Collections.Generic;
using System.Linq;
using Tesearis.HlslParser.Diagnostics;
using Tesearis.HlslParser.Lexing;
using Tesearis.HlslParser.Parsing;
using Tesearis.HlslParser.Syntax;
using Tesearis.HlslParser.Text;
using NUnit.Framework;

namespace Tesearis.HlslParser.Tests.Parsing
{
    [TestFixture]
    public class ParserTests
    {
        private static CompilationUnitNode ParseUnit(string text, out HlslParseResult result)
        {
            result = Hlsl.Parse(text, "test.hlsl");
            return (CompilationUnitNode)result.Root;
        }

        private static CompilationUnitNode ParseUnit(string text)
        {
            return ParseUnit(text, out _);
        }
        
        [Test]
        public void ParsesBasicStruct()
        {
            var unit = ParseUnit("struct S { float4 x; };", out var result);

            Assert.IsFalse(result.HasErrors);
            var s = (StructDeclarationNode)unit.Declarations[0];
            Assert.AreEqual("S", s.Name);
            Assert.IsFalse(s.IsMissingBody);
            Assert.AreEqual(1, s.Fields.Count);
            var field = (StructFieldNode)s.Fields[0];
            Assert.AreEqual("float4", field.Type.Name);
            Assert.AreEqual("x", field.Declarators[0].Name);
        }

        [Test]
        public void ParsesStructFieldWithArrayRank()
        {
            var unit = ParseUnit("struct S { float4 x[4]; };");
            var field = (StructFieldNode)((StructDeclarationNode)unit.Declarations[0]).Fields[0];
            var declarator = field.Declarators[0];

            Assert.AreEqual(1, declarator.ArrayRanks.Count);
            Assert.AreEqual(4, declarator.ArrayRanks[0].ConstantSize);
        }

        [Test]
        public void ParsesStructFieldWithMultipleDeclarators()
        {
            var unit = ParseUnit("struct S { float a, b, c; };");
            var field = (StructFieldNode)((StructDeclarationNode)unit.Declarations[0]).Fields[0];

            Assert.AreEqual(3, field.Declarators.Count);
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, field.Declarators.Select(d => d.Name));
        }

        [Test]
        public void ParsesEmptyStruct()
        {
            var unit = ParseUnit("struct S {};", out var result);
            var s = (StructDeclarationNode)unit.Declarations[0];

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual(0, s.Fields.Count);
        }

        [Test]
        public void StructRecoversFromMalformedMember()
        {
            var unit = ParseUnit("struct S { 123; float4 x; };", out var result);
            var s = (StructDeclarationNode)unit.Declarations[0];

            Assert.IsTrue(result.HasErrors);
            Assert.IsInstanceOf<ErrorNode>(s.Fields[0]);
            var field = (StructFieldNode)s.Fields[1];
            Assert.AreEqual("x", field.Declarators[0].Name);
        }
        
        [Test]
        public void ParsesCbufferWithoutRegister()
        {
            var unit = ParseUnit("cbuffer PerFrame { float4 _Color; };", out var result);
            var cb = (CbufferDeclarationNode)unit.Declarations[0];

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual("PerFrame", cb.Name);
            Assert.IsNull(cb.RegisterClause);
            Assert.AreEqual(1, cb.Members.Count);
        }

        [Test]
        public void ParsesCbufferWithRegister()
        {
            var unit = ParseUnit("cbuffer PerFrame : register(b0) { float4 _Color; };");
            var cb = (CbufferDeclarationNode)unit.Declarations[0];

            Assert.IsNotNull(cb.RegisterClause);
            Assert.AreEqual("b0", cb.RegisterClause.RegisterSlot);
        }

        [Test]
        public void ParsesCbufferWithMultipleMembers()
        {
            var unit = ParseUnit("cbuffer PerFrame { float4 a; float4 b; };");
            var cb = (CbufferDeclarationNode)unit.Declarations[0];
            Assert.AreEqual(2, cb.Members.Count);
        }

        [Test]
        public void ParsesTbufferLikeCbuffer()
        {
            var unit = ParseUnit("tbuffer PerFrame { float4 a; };", out var result);
            Assert.IsFalse(result.HasErrors);
            Assert.IsInstanceOf<CbufferDeclarationNode>(unit.Declarations[0]);
        }

        [Test]
        public void ConstantBufferTemplateParsesAsGlobalVariableNotCbufferDeclaration()
        {
            var unit = ParseUnit("ConstantBuffer<PerFrameData> cb0 : register(b0);", out var result);

            Assert.IsFalse(result.HasErrors);
            Assert.IsInstanceOf<GlobalVariableDeclarationNode>(unit.Declarations[0]);
            var global = (GlobalVariableDeclarationNode)unit.Declarations[0];
            Assert.AreEqual("ConstantBuffer", global.Type.Name);
            Assert.AreEqual("PerFrameData", global.Type.TypeArguments[0].Name);
            Assert.AreEqual("cb0", global.Declarators[0].Name);
            Assert.AreEqual("b0", global.Declarators[0].RegisterClause.RegisterSlot);
        }
        
        [TestCase("float x;", "float")]
        [TestCase("float4 x;", "float4")]
        [TestCase("float4x4 x;", "float4x4")]
        [TestCase("Texture2D x;", "Texture2D")]
        [TestCase("MyStruct x;", "MyStruct")]
        public void ParsesGlobalVariableAcrossTypeCategories(string source, string expectedTypeName)
        {
            var unit = ParseUnit(source, out var result);
            Assert.IsFalse(result.HasErrors);
            var global = (GlobalVariableDeclarationNode)unit.Declarations[0];
            Assert.AreEqual(expectedTypeName, global.Type.Name);
        }

        [Test]
        public void ParsesTemplatedResourceType()
        {
            var unit = ParseUnit("StructuredBuffer<float4> _Buf;");
            var global = (GlobalVariableDeclarationNode)unit.Declarations[0];
            Assert.AreEqual("StructuredBuffer", global.Type.Name);
            Assert.AreEqual("float4", global.Type.TypeArguments[0].Name);
        }

        [Test]
        public void ParsesGlobalVariableArray()
        {
            var unit = ParseUnit("float4 _Colors[4];");
            var declarator = ((GlobalVariableDeclarationNode)unit.Declarations[0]).Declarators[0];
            Assert.AreEqual(4, declarator.ArrayRanks[0].ConstantSize);
        }

        [Test]
        public void ParsesGlobalVariableWithMultipleDeclarators()
        {
            var unit = ParseUnit("float a, b, c;");
            var global = (GlobalVariableDeclarationNode)unit.Declarations[0];
            CollectionAssert.AreEqual(new[] { "a", "b", "c" }, global.Declarators.Select(d => d.Name));
        }

        [Test]
        public void ParsesRegisterClause()
        {
            var unit = ParseUnit("Texture2D _Tex : register(t0);");
            var declarator = ((GlobalVariableDeclarationNode)unit.Declarations[0]).Declarators[0];
            Assert.AreEqual("t0", declarator.RegisterClause.RegisterSlot);
            Assert.IsNull(declarator.RegisterClause.RegisterSpace);
        }

        [Test]
        public void ParsesRegisterClauseWithSpace()
        {
            var unit = ParseUnit("Texture2D _Tex : register(t0, space1);");
            var declarator = ((GlobalVariableDeclarationNode)unit.Declarations[0]).Declarators[0];
            Assert.AreEqual("space1", declarator.RegisterClause.RegisterSpace);
        }

        [Test]
        public void ParsesPackoffsetClause()
        {
            var unit = ParseUnit("cbuffer C { float4 x : packoffset(c0.x); };");
            var member = (GlobalVariableDeclarationNode)((CbufferDeclarationNode)unit.Declarations[0]).Members[0];
            var declarator = member.Declarators[0];
            Assert.AreEqual("c0", declarator.PackoffsetClause.Offset);
            Assert.AreEqual("x", declarator.PackoffsetClause.ComponentSwizzle);
        }

        [Test]
        public void ParsesRegisterAndPackoffsetTogether()
        {
            var unit = ParseUnit("cbuffer C { float4 x : packoffset(c0.x) : register(c0); };");
            var member = (GlobalVariableDeclarationNode)((CbufferDeclarationNode)unit.Declarations[0]).Members[0];
            var declarator = member.Declarators[0];
            Assert.IsNotNull(declarator.PackoffsetClause);
            Assert.IsNotNull(declarator.RegisterClause);
        }

        [Test]
        public void ParsesModifierCombinations()
        {
            var unit = ParseUnit("static const float3 k = float3(1, 2, 3);", out var result);
            var global = (GlobalVariableDeclarationNode)unit.Declarations[0];

            Assert.IsFalse(result.HasErrors);
            CollectionAssert.AreEqual(new[] { "static", "const" }, global.Modifiers);
        }

        [Test]
        public void NestedCommaInitializerDoesNotBreakDeclaratorList()
        {
            var unit = ParseUnit("static const float3 k = float3(1, 2, 3), other = 0;", out var result);
            var global = (GlobalVariableDeclarationNode)unit.Declarations[0];

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual(2, global.Declarators.Count);
            Assert.AreEqual("k", global.Declarators[0].Name);
            Assert.AreEqual("other", global.Declarators[1].Name);
            Assert.IsNotNull(global.Declarators[0].Initializer);
        }
        
        [Test]
        public void ParsesTypedefWithoutArrayRank()
        {
            var unit = ParseUnit("typedef float3 Position;", out var result);
            var td = (TypedefDeclarationNode)unit.Declarations[0];

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual("float3", td.UnderlyingType.Name);
            Assert.AreEqual("Position", td.AliasName);
            Assert.AreEqual(0, td.ArrayRanks.Count);
        }

        [Test]
        public void ParsesTypedefWithArrayRank()
        {
            var unit = ParseUnit("typedef float4 Float4x4Row[4];");
            var td = (TypedefDeclarationNode)unit.Declarations[0];

            Assert.AreEqual(1, td.ArrayRanks.Count);
            Assert.AreEqual(4, td.ArrayRanks[0].ConstantSize);
        }
        
        [Test]
        public void ParsesFunctionWithNoParameters()
        {
            var unit = ParseUnit("void Foo() {}", out var result);
            var fn = (FunctionDeclarationNode)unit.Declarations[0];

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual("Foo", fn.Name);
            Assert.AreEqual(0, fn.Parameters.Count);
            Assert.IsFalse(fn.IsForwardDeclaration);
        }

        [Test]
        public void ParsesParameterModifiers()
        {
            var unit = ParseUnit("void Foo(in float3 a, out float3 b, inout float3 c) {}");
            var fn = (FunctionDeclarationNode)unit.Declarations[0];

            CollectionAssert.AreEqual(new[] { "in" }, ((ParameterNode)fn.Parameters[0]).Modifiers);
            CollectionAssert.AreEqual(new[] { "out" }, ((ParameterNode)fn.Parameters[1]).Modifiers);
            CollectionAssert.AreEqual(new[] { "inout" }, ((ParameterNode)fn.Parameters[2]).Modifiers);
        }

        [Test]
        public void ParsesParameterSemantic()
        {
            var unit = ParseUnit("void Foo(float4 pos : SV_Position) {}");
            var parameter = (ParameterNode)((FunctionDeclarationNode)unit.Declarations[0]).Parameters[0];
            Assert.AreEqual("SV_Position", parameter.Semantic.Name);
        }

        [Test]
        public void ParsesReturnSemantic()
        {
            var unit = ParseUnit("float4 Frag() : SV_Target { return 0; }");
            var fn = (FunctionDeclarationNode)unit.Declarations[0];
            Assert.AreEqual("SV_Target", fn.ReturnSemantic.Name);
        }

        [Test]
        public void ParsesNumthreadsAttribute()
        {
            var unit = ParseUnit("[numthreads(8,8,1)]\nvoid CSMain() {}");
            var fn = (FunctionDeclarationNode)unit.Declarations[0];

            Assert.AreEqual(1, fn.Attributes.Count);
            Assert.AreEqual("numthreads", fn.Attributes[0].Name);
            CollectionAssert.AreEqual(new[] { "8", "8", "1" }, fn.Attributes[0].Arguments.Select(a => a.RawText));
        }

        [Test]
        public void ParsesMultipleStackedAttributes()
        {
            var unit = ParseUnit("[unroll]\n[maxvertexcount(4)]\nvoid Foo() {}");
            var fn = (FunctionDeclarationNode)unit.Declarations[0];

            Assert.AreEqual(2, fn.Attributes.Count);
            Assert.AreEqual("unroll", fn.Attributes[0].Name);
            Assert.AreEqual("maxvertexcount", fn.Attributes[1].Name);
        }

        [Test]
        public void ParsesForwardDeclaration()
        {
            var unit = ParseUnit("void Foo(float3 a);", out var result);
            var fn = (FunctionDeclarationNode)unit.Declarations[0];

            Assert.IsFalse(result.HasErrors);
            Assert.IsTrue(fn.IsForwardDeclaration);
            Assert.IsNull(fn.Body);
        }

        [Test]
        public void ParsesUnnamedPrototypeParameterWithoutDiagnostic()
        {
            var unit = ParseUnit("void Foo(float3);", out var result);
            var fn = (FunctionDeclarationNode)unit.Declarations[0];

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual(string.Empty, ((ParameterNode)fn.Parameters[0]).Name);
        }

        [Test]
        public void ParsesEmptyParameterList()
        {
            var unit = ParseUnit("void Foo() {}");
            var fn = (FunctionDeclarationNode)unit.Declarations[0];
            Assert.AreEqual(0, fn.Parameters.Count);
        }
        
        [Test]
        public void MissingSemicolonInsideStructRecoversToNextDeclaration()
        {
            var unit = ParseUnit("struct S { float4 x }\nstruct T { float4 y; };", out var result);

            Assert.IsTrue(result.HasErrors);
            Assert.AreEqual(2, unit.Declarations.Count);
            var t = (StructDeclarationNode)unit.Declarations[1];
            Assert.AreEqual("T", t.Name);
            Assert.AreEqual("y", ((StructFieldNode)t.Fields[0]).Declarators[0].Name);
        }

        [Test]
        public void MissingSemicolonAfterGlobalVariableRecoversToNextDeclaration()
        {
            var unit = ParseUnit("float4 a\nfloat4 b;", out var result);

            Assert.IsTrue(result.HasErrors);
            var b = (GlobalVariableDeclarationNode)unit.Declarations.Last(d => d is GlobalVariableDeclarationNode g && g.Declarators[0].Name == "b");
            Assert.AreEqual("b", b.Declarators[0].Name);
        }

        [Test]
        public void GarbageTopLevelTokenBecomesErrorNodeAndSubsequentDeclarationsStillParse()
        {
            var unit = ParseUnit("@@@\nfloat4 a;", out var result);

            Assert.IsTrue(result.HasErrors);
            Assert.IsInstanceOf<ErrorNode>(unit.Declarations[0]);
            var global = (GlobalVariableDeclarationNode)unit.Declarations[1];
            Assert.AreEqual("a", global.Declarators[0].Name);
        }

        [Test]
        public void UnterminatedStructAtEofYieldsMissingBodyNoException()
        {
            HlslParseResult result = null;
            Assert.DoesNotThrow(() => result = Hlsl.Parse("struct S", "test.hlsl"));

            var unit = (CompilationUnitNode)result.Root;
            var s = (StructDeclarationNode)unit.Declarations[0];
            Assert.IsTrue(s.IsMissingBody);
            Assert.IsTrue(result.HasErrors);
        }

        [Test]
        public void UnterminatedStructBodyAtEofYieldsUnterminatedStructDiagnosticNoException()
        {
            HlslParseResult result = null;
            Assert.DoesNotThrow(() => result = Hlsl.Parse("struct S { float4 x;", "test.hlsl"));

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.UnterminatedStruct));
        }

        [Test]
        public void UnterminatedCbufferAtEofYieldsUnterminatedCbufferDiagnosticNoException()
        {
            HlslParseResult result = null;
            Assert.DoesNotThrow(() => result = Hlsl.Parse("cbuffer C { float4 x;", "test.hlsl"));

            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.UnterminatedCbuffer));
        }

        [Test]
        public void UnterminatedFunctionBodyAtEofYieldsNoException()
        {
            HlslParseResult result = null;
            Assert.DoesNotThrow(() => result = Hlsl.Parse("void Foo() { float x = 1;", "test.hlsl"));

            var unit = (CompilationUnitNode)result.Root;
            var fn = (FunctionDeclarationNode)unit.Declarations[0];
            Assert.IsNotNull(fn.Body);
            Assert.IsTrue(result.HasErrors);
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.UnterminatedBlock));
        }
        
        [Test]
        public void MissingStructBraceReportsExpectedToken()
        {
            var result = Hlsl.Parse("struct S float4 x;", "test.hlsl");
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.ExpectedToken));
        }

        [Test]
        public void GarbageTopLevelTokenReportsExpectedDeclaration()
        {
            var result = Hlsl.Parse("@@@", "test.hlsl");
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.ExpectedDeclaration));
        }

        [Test]
        public void MalformedRegisterClauseReportsMalformedRegisterClause()
        {
            var result = Hlsl.Parse("Texture2D _Tex : register;", "test.hlsl");
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.MalformedRegisterClause));
        }

        [Test]
        public void MalformedPackoffsetClauseReportsMalformedPackoffsetClause()
        {
            var result = Hlsl.Parse("cbuffer C { float4 x : packoffset; };", "test.hlsl");
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.MalformedPackoffsetClause));
        }

        [Test]
        public void MalformedSemanticReportsMalformedSemantic()
        {
            var result = Hlsl.Parse("struct S { float4 x : ; };", "test.hlsl");
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.MalformedSemantic));
        }

        [Test]
        public void UnterminatedParameterListReportsUnterminatedParameterList()
        {
            var result = Hlsl.Parse("void Foo(float3 a", "test.hlsl");
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.UnterminatedParameterList));
        }

        [Test]
        public void MalformedAttributeReportsMalformedAttribute()
        {
            var result = Hlsl.Parse("[numthreads(8,8,1)\nvoid CSMain() {}", "test.hlsl");
            Assert.IsTrue(result.Diagnostics.Any(d => d.Id == DiagnosticIds.MalformedAttribute));
        }
        
        [Test]
        public void ConstructorThrowsOnNullSource()
        {
            var source = new SourceText("float4 x;", "test.hlsl");
            var tokens = new Lexer(source, new DiagnosticSink(source)).Tokenize();
            Assert.Throws<ArgumentNullException>(() => new Parser(null, tokens, new DiagnosticSink(source)));
        }

        [Test]
        public void ConstructorThrowsOnNullTokens()
        {
            var source = new SourceText("float4 x;", "test.hlsl");
            Assert.Throws<ArgumentNullException>(() => new Parser(source, null, new DiagnosticSink(source)));
        }

        [Test]
        public void ConstructorThrowsOnEmptyTokenList()
        {
            var source = new SourceText("float4 x;", "test.hlsl");
            Assert.Throws<ArgumentException>(() => new Parser(source, new List<Token>(), new DiagnosticSink(source)));
        }

        [Test]
        public void ParsesFixedPrecisionTypesInFunction()
        {
            var unit = ParseUnit("fixed4 main(fixed2 uv : TEXCOORD0) : SV_Target { fixed3 c = fixed3(1, 0, 0); return fixed4(c, 1); }", out var result);

            Assert.IsFalse(result.HasErrors);
            var fn = (FunctionDeclarationNode)unit.Declarations[0];
            Assert.AreEqual("fixed4", fn.ReturnType.Name);
            Assert.AreEqual(HlslKeywordCategory.VectorType, fn.ReturnType.Category);
            var param = (ParameterNode)fn.Parameters[0];
            Assert.AreEqual("fixed2", param.Type.Name);
            Assert.AreEqual("uv", param.Name);
            Assert.AreEqual("TEXCOORD0", param.Semantic.Name);
        }

        [Test]
        public void ParsesGeometryShaderWithTriangleInputAndStreamOutput()
        {
            var unit = ParseUnit("[maxvertexcount(3)] void GS(triangle VertexIn input[3], inout TriangleStream<VertexOut> outStream) { }", out var result);

            Assert.IsFalse(result.HasErrors);
            var fn = (FunctionDeclarationNode)unit.Declarations[0];
            Assert.AreEqual(1, fn.Attributes.Count);
            Assert.AreEqual("maxvertexcount", fn.Attributes[0].Name);

            var inParam = (ParameterNode)fn.Parameters[0];
            CollectionAssert.AreEqual(new[] { "triangle" }, inParam.Modifiers);
            Assert.AreEqual("VertexIn", inParam.Type.Name);
            Assert.AreEqual("input", inParam.Name);
            Assert.AreEqual(1, inParam.ArrayRanks.Count);
            Assert.AreEqual(3, inParam.ArrayRanks[0].ConstantSize);

            var outParam = (ParameterNode)fn.Parameters[1];
            CollectionAssert.AreEqual(new[] { "inout" }, outParam.Modifiers);
            Assert.AreEqual("TriangleStream", outParam.Type.Name);
            Assert.AreEqual(1, outParam.Type.TypeArguments.Count);
            Assert.AreEqual("VertexOut", outParam.Type.TypeArguments[0].Name);
            Assert.AreEqual("outStream", outParam.Name);
        }

        [Test]
        public void ParsesTessellationInputPatchWithMultiArgumentTemplate()
        {
            var unit = ParseUnit("HSConstantDataOutput HSConst(InputPatch<VSOutput, 3> patch) { }", out var result);

            Assert.IsFalse(result.HasErrors);
            var fn = (FunctionDeclarationNode)unit.Declarations[0];
            var param = (ParameterNode)fn.Parameters[0];
            Assert.AreEqual("InputPatch", param.Type.Name);
            Assert.AreEqual(2, param.Type.TypeArguments.Count);
            Assert.AreEqual("VSOutput", param.Type.TypeArguments[0].Name);
            Assert.AreEqual("3", param.Type.TypeArguments[1].Name);
            Assert.AreEqual("patch", param.Name);
        }

        [Test]
        public void ParsesRayTracingGlobalVariableAndLocalQuery()
        {
            var unit = ParseUnit(@"
                RaytracingAccelerationStructure g_AccelStruct : register(t0);
                void Trace() {
                    RayQuery<RAY_FLAG_NONE> q;
                    RayDesc ray;
                }", out var result);

            Assert.IsFalse(result.HasErrors);
            var global = (GlobalVariableDeclarationNode)unit.Declarations[0];
            Assert.AreEqual("RaytracingAccelerationStructure", global.Type.Name);
            Assert.AreEqual("g_AccelStruct", global.Declarators[0].Name);
            Assert.AreEqual("t0", global.Declarators[0].RegisterClause.RegisterSlot);

            var fn = (FunctionDeclarationNode)unit.Declarations[1];
            var body = (BlockStatementNode)fn.Body;
            var decl1 = (DeclarationStatementNode)body.Statements[0];
            Assert.AreEqual("RayQuery", decl1.Type.Name);
            Assert.AreEqual(1, decl1.Type.TypeArguments.Count);
            Assert.AreEqual("RAY_FLAG_NONE", decl1.Type.TypeArguments[0].Name);

            var decl2 = (DeclarationStatementNode)body.Statements[1];
            Assert.AreEqual("RayDesc", decl2.Type.Name);
            Assert.AreEqual(HlslKeywordCategory.ScalarType, decl2.Type.Category);
        }

        [Test]
        public void ParsesGenericVectorAndMatrixTypes()
        {
            var unit = ParseUnit(@"
                vector<float, 4> v;
                matrix<half, 3, 3> m;
            ", out var result);

            Assert.IsFalse(result.HasErrors);
            var v = (GlobalVariableDeclarationNode)unit.Declarations[0];
            Assert.AreEqual("vector", v.Type.Name);
            Assert.AreEqual(2, v.Type.TypeArguments.Count);
            Assert.AreEqual("float", v.Type.TypeArguments[0].Name);
            Assert.AreEqual("4", v.Type.TypeArguments[1].Name);

            var m = (GlobalVariableDeclarationNode)unit.Declarations[1];
            Assert.AreEqual("matrix", m.Type.Name);
            Assert.AreEqual(3, m.Type.TypeArguments.Count);
            Assert.AreEqual("half", m.Type.TypeArguments[0].Name);
            Assert.AreEqual("3", m.Type.TypeArguments[1].Name);
            Assert.AreEqual("3", m.Type.TypeArguments[2].Name);
        }

        [Test]
        public void ParsesNestedTemplatesWithConsecutiveClosingAngleBrackets()
        {
            var unit = ParseUnit("StructuredBuffer<vector<float, 4>> buf;", out var result);

            Assert.IsFalse(result.HasErrors);
            var global = (GlobalVariableDeclarationNode)unit.Declarations[0];
            Assert.AreEqual("StructuredBuffer", global.Type.Name);
            Assert.AreEqual(1, global.Type.TypeArguments.Count);
            var inner = global.Type.TypeArguments[0];
            Assert.AreEqual("vector", inner.Name);
            Assert.AreEqual(2, inner.TypeArguments.Count);
            Assert.AreEqual("float", inner.TypeArguments[0].Name);
            Assert.AreEqual("4", inner.TypeArguments[1].Name);
        }

        [Test]
        public void ParsesRovsAndSubpassInputsAndExtendedSamplers()
        {
            var unit = ParseUnit(@"
                globallycoherent RasterizerOrderedTexture2D<float4> g_ROV;
                SubpassInput<float4> g_Subpass;
                sampler2DMS g_Sampler;
            ", out var result);

            Assert.IsFalse(result.HasErrors);
            var rov = (GlobalVariableDeclarationNode)unit.Declarations[0];
            CollectionAssert.AreEqual(new[] { "globallycoherent" }, rov.Modifiers);
            Assert.AreEqual("RasterizerOrderedTexture2D", rov.Type.Name);
            Assert.AreEqual("float4", rov.Type.TypeArguments[0].Name);

            var subpass = (GlobalVariableDeclarationNode)unit.Declarations[1];
            Assert.AreEqual("SubpassInput", subpass.Type.Name);
            Assert.AreEqual("float4", subpass.Type.TypeArguments[0].Name);

            var sampler = (GlobalVariableDeclarationNode)unit.Declarations[2];
            Assert.AreEqual("sampler2DMS", sampler.Type.Name);
        }
    }
}
