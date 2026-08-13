# HlslParser

Standalone HLSL/Cg lexer, preprocessor and syntax tree for shader source.

`HlslParser` parses HLSL/Cg source, either a whole standalone file (`.hlsl`, `.cginc`,`.compute`) or a region embedded inside a document, into an
immutable, strongly-typed AST for static analysis and diagnostics tooling.

## Features

- **Zero dependencies**: no runtime dependencies at all.
- **Standalone and embeddable**: parses a whole `.hlsl`/`.cginc`/`.compute` file directly, or a substring embedded inside a document.
- **Full HLSL/Cg grammar**: structs, buffers, resources, functions and full statement/expression parsing inside function bodies.
- **Real preprocessor**: genuine `#define`/`#undef` macro expansion and `#if`/`#ifdef`/`#ifndef`/`#elif`/`#else`/`#endif` conditional evaluation,
  since both are self-contained within a single file's text.
- **Best-effort recovery**: never throws on malformed source; it returns a (possibly partial) tree plus a full diagnostics list. That
  makes it suitable for live analysis over source that's routinely mid-edit, in addition to normal batch/CI parsing.
- **Targeted**: `netstandard2.0` (for use in the Unity Editor) and `net8.0`.

## What this library does *not* do

- It never reads files from disk.
- It never resolves `#include` targets.
- It doesn't recognize casts to user-defined type names (only built-in ones).

## Usage

### Parsing a standalone file

```csharp
using HlslParser.Parsing;
using HlslParser.Syntax;

HlslParseResult result = Hlsl.Parse(sourceText, "shader.hlsl");

if (result.HasErrors)
{
    foreach (var diagnostic in result.Diagnostics)
    {
        Console.WriteLine(diagnostic);
    }
}

var compilationUnit = (CompilationUnitNode)result.Root;
foreach (var declaration in compilationUnit.Declarations)
{
    Console.WriteLine(declaration.Kind);
}
```

`Hlsl.Parse` never throws on malformed input — it always returns a (possibly partial) tree in `result.Root` alongside `result.Diagnostics`.

### Parsing an embedded region

Use `Hlsl.ParseEmbedded` when the HLSL/Cg source is a substring of a larger document (e.g. a Unity ShaderLab `HLSLPROGRAM` block). Spans and
diagnostics are reported in terms of the *outer* document, using the offset/line where the embedded block starts:

```csharp
HlslParseResult result = Hlsl.ParseEmbedded(
    body: hlslBlockText,
    baseOffset: hlslBlockStartOffset,
    fileName: "MyShader.shader",
    baseLine: hlslBlockStartLine);
```

### Walking the syntax tree

For quick queries, use LINQ over `DescendantsAndSelf()`:

```csharp
using System.Linq;

var functions = result.Root.DescendantsAndSelf().OfType<FunctionDeclarationNode>();
```

For more structured traversal, subclass `HlslVisitor` and override the node kinds you care about:

```csharp
class FunctionCollector : HlslVisitor
{
    public List<FunctionDeclarationNode> Functions { get; } = new();

    public override void VisitFunctionDeclaration(FunctionDeclarationNode node)
    {
        Functions.Add(node);
        base.DefaultVisit(node);
    }
}

var collector = new FunctionCollector();
collector.Visit(result.Root);
```

To inspect the whole tree at a glance, `HlslTreeDumper.Dump` renders it as indented text:

```csharp
string dump = HlslTreeDumper.Dump(result.Root, result.Source);
```

### Diagnostics

```csharp
using HlslParser.Diagnostics;

foreach (Diagnostic diagnostic in result.Diagnostics)
{
    // diagnostic.Severity, diagnostic.Id, diagnostic.Message, diagnostic.Span
    Console.WriteLine(diagnostic); // "shader.hlsl(3,10): error HL0203: ..."
}
```

## Building & testing

```
dotnet build HlslParser.slnx
dotnet test HlslParser.slnx
```

## License

MIT — see [LICENSE](LICENSE).
