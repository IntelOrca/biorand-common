using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IntelOrca.Biohazard.BioRand.Graphing
{
    public class MermaidBuilder
    {
        private readonly StringBuilder _sb = new StringBuilder();
        private int _indent;

        public MermaidBuilder()
        {
            AppendLine("flowchart TD");
            Indent();
        }

        public override string ToString() => _sb.ToString();

        public void BeginSubgraph(string label)
        {
            AppendLine($"subgraph \"{label}\"");
            Indent();
        }

        public void EndSubgraph()
        {
            Unindent();
            AppendLine($"end");
        }

        public void Node(string name, string? label = null, MermaidShape shape = MermaidShape.Square)
        {
            var (beginShape, endShape) = GetShape(shape);
            AppendLine($"{name}{beginShape}\"{label ?? name}\"{endShape}");
        }

        public void Edge(string source, string target, string? label = null, MermaidEdgeType type = MermaidEdgeType.Solid)
        {
            var (full, left, right) = GetEdgeType(type);
            if (string.IsNullOrEmpty(label))
                AppendLine($"{source} {full} {target}");
            else
                AppendLine($"{source} {left} \"{label}\" {right} {target}");
        }

        public void ClassDefinition(string name, Dictionary<string, string> css)
        {
            AppendLine($"classDef {name} {string.Join(",", css.Select(x => $"{x.Key}:{x.Value}"))};");
        }

        public void Class(string name, IEnumerable<string> nodes)
        {
            AppendLine($"class {string.Join(",", nodes)} {name}");
        }

        private void Indent() => _indent++;
        private void Unindent() => _indent--;

        private void AppendLine(string text)
        {
            _sb.Append(' ', _indent * 4);
            _sb.Append(text);
            _sb.Append('\n');
        }

        private static (string, string) GetShape(MermaidShape shape)
        {
            return shape switch
            {
                MermaidShape.Square => ("[", "]"),
                MermaidShape.DoubleSquare => ("[[", "]]"),
                MermaidShape.Rounded => ("(", ")"),
                MermaidShape.Circle => ("((", "))"),
                MermaidShape.Hexagon => ("{{", "}}"),
                _ => throw new ArgumentException("Invalid shape", nameof(shape))
            };
        }

        private static (string, string, string) GetEdgeType(MermaidEdgeType type)
        {
            return type switch
            {
                MermaidEdgeType.Solid => ("-->", "--", "-->"),
                MermaidEdgeType.Dotted => ("-.->", "-.", ".->"),
                MermaidEdgeType.Bidirectional | MermaidEdgeType.Solid => ("<-->", "<--", "-->"),
                MermaidEdgeType.Bidirectional | MermaidEdgeType.Dotted => ("<-.->", "<-.", ".->"),
                _ => throw new ArgumentException("Invalid type", nameof(type))
            };
        }
    }

    public enum MermaidShape
    {
        Square,
        DoubleSquare,
        Rounded,
        Circle,
        Hexagon
    }

    [Flags]
    public enum MermaidEdgeType
    {
        Solid,
        Dotted = 1 << 0,
        Bidirectional = 1 << 1,
        Thick = 1 << 2
    }
}
