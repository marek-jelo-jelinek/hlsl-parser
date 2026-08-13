using System.Linq;
using HlslParser.Parsing;
using HlslParser.Syntax;
using NUnit.Framework;

namespace HlslParser.Tests.Syntax
{
    /// <summary>Golden-file tests over the public <see cref="Hlsl.Parse"/> pipeline: a
    /// compute-kernel signature, a vertex/fragment struct pair, and a deliberately broken struct
    /// that still yields a partial tree plus diagnostics. Fixtures are synthetic — not copied
    /// from Unity engine source.</summary>
    [TestFixture]
    public class HlslTreeDumperTests
    {
        private const string ComputeKernelSource = @"
RWStructuredBuffer<float4> _Result : register(u0);

cbuffer Params : register(b0)
{
    float4 _Scale;
};

[numthreads(8,8,1)]
void CSMain(uint3 id : SV_DispatchThreadID)
{
    _Result[id.x] = _Scale;
}
";

        private const string VertexFragmentSource = @"
struct Attributes
{
    float3 positionOS : POSITION;
    float2 uv : TEXCOORD0;
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float2 uv : TEXCOORD0;
};

Varyings Vert(Attributes input)
{
    Varyings output;
    return output;
}

float4 Frag(Varyings input) : SV_Target
{
    return float4(1, 1, 1, 1);
}
";

        private const string ComputeKernelDump =
            "CompilationUnit (3)   @2:1\n" +
            "  GlobalVariable   @2:1\n" +
            "    TypeName RWStructuredBuffer   @2:1\n" +
            "      TypeName float4   @2:20\n" +
            "    Declarator _Result   @2:28\n" +
            "      Register u0   @2:36\n" +
            "  Cbuffer Params   @4:1\n" +
            "    Register b0   @4:16\n" +
            "    GlobalVariable   @6:5\n" +
            "      TypeName float4   @6:5\n" +
            "      Declarator _Scale   @6:12\n" +
            "  Function CSMain   @9:1\n" +
            "    Attribute [numthreads]   @9:1\n" +
            "      Argument 8   @9:13\n" +
            "        Literal 8   @9:13\n" +
            "      Argument 8   @9:15\n" +
            "        Literal 8   @9:15\n" +
            "      Argument 1   @9:17\n" +
            "        Literal 1   @9:17\n" +
            "    TypeName void   @10:1\n" +
            "    Parameter id   @10:13\n" +
            "      TypeName uint3   @10:13\n" +
            "      Semantic SV_DispatchThreadID   @10:22\n" +
            "    Block (1)   @11:1\n" +
            "      ExpressionStatement   @12:5\n" +
            "        Assignment Equals   @12:5\n" +
            "          ElementAccess   @12:5\n" +
            "            Identifier _Result   @12:5\n" +
            "            MemberAccess .x   @12:13\n" +
            "              Identifier id   @12:13\n" +
            "          Identifier _Scale   @12:21\n";

        private const string VertexFragmentDump =
            "CompilationUnit (4)   @2:1\n" +
            "  Struct Attributes   @2:1\n" +
            "    Field   @4:5\n" +
            "      TypeName float3   @4:5\n" +
            "      Declarator positionOS   @4:12\n" +
            "        Semantic POSITION   @4:23\n" +
            "    Field   @5:5\n" +
            "      TypeName float2   @5:5\n" +
            "      Declarator uv   @5:12\n" +
            "        Semantic TEXCOORD0   @5:15\n" +
            "  Struct Varyings   @8:1\n" +
            "    Field   @10:5\n" +
            "      TypeName float4   @10:5\n" +
            "      Declarator positionCS   @10:12\n" +
            "        Semantic SV_POSITION   @10:23\n" +
            "    Field   @11:5\n" +
            "      TypeName float2   @11:5\n" +
            "      Declarator uv   @11:12\n" +
            "        Semantic TEXCOORD0   @11:15\n" +
            "  Function Vert   @14:1\n" +
            "    TypeName Varyings (user)   @14:1\n" +
            "    Parameter input   @14:15\n" +
            "      TypeName Attributes (user)   @14:15\n" +
            "    Block (2)   @15:1\n" +
            "      DeclarationStatement   @16:5\n" +
            "        TypeName Varyings (user)   @16:5\n" +
            "        Declarator output   @16:14\n" +
            "      Return   @17:5\n" +
            "        Identifier output   @17:12\n" +
            "  Function Frag   @20:1\n" +
            "    TypeName float4   @20:1\n" +
            "    Parameter input   @20:13\n" +
            "      TypeName Varyings (user)   @20:13\n" +
            "    Semantic SV_Target   @20:29\n" +
            "    Block (1)   @21:1\n" +
            "      Return   @22:5\n" +
            "        Invocation   @22:12\n" +
            "          Identifier float4   @22:12\n" +
            "          Literal 1   @22:19\n" +
            "          Literal 1   @22:22\n" +
            "          Literal 1   @22:25\n" +
            "          Literal 1   @22:28\n";

        [Test]
        public void DumpsComputeKernelSignature()
        {
            var result = Hlsl.Parse(ComputeKernelSource, "compute.hlsl");
            var dump = HlslTreeDumper.Dump(result.Root, result.Source);

            Assert.AreEqual(ComputeKernelDump, dump);
            Assert.IsFalse(result.HasErrors);
        }

        [Test]
        public void DumpsVertexFragmentStructPair()
        {
            var result = Hlsl.Parse(VertexFragmentSource, "vertfrag.hlsl");
            var dump = HlslTreeDumper.Dump(result.Root, result.Source);

            Assert.AreEqual(VertexFragmentDump, dump);
            Assert.IsFalse(result.HasErrors);
        }

        [Test]
        public void BrokenStructStillProducesPartialTreeWithDiagnostics()
        {
            const string source = @"
struct Broken
{
    float3 position : POSITION
    123;
    float2 uv : TEXCOORD0;
};
";
            HlslParseResult result = null;
            Assert.DoesNotThrow(() => result = Hlsl.Parse(source, "broken.hlsl"));

            var unit = (CompilationUnitNode)result.Root;
            Assert.AreEqual(1, unit.Declarations.Count);
            Assert.IsInstanceOf<StructDeclarationNode>(unit.Declarations[0]);
            var structDecl = (StructDeclarationNode)unit.Declarations[0];

            Assert.IsTrue(structDecl.Fields.OfType<StructFieldNode>().Any());
            Assert.IsTrue(structDecl.Fields.OfType<ErrorNode>().Any());
            Assert.IsTrue(result.HasErrors);
        }
    }
}
