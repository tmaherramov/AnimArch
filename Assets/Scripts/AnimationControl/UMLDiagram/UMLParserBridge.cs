using AnimationControl.OAL;
using Antlr4.Runtime;
using OALProgramControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnimationControl.UMLDiagram;

namespace Assets.Scripts.AnimationControl.UMLDiagram
{
    public class UMLParserBridge
    {
        public static ClassDiagramManager Parse(String Code)
        {
            ICharStream target = new AntlrInputStream(Code);
            ITokenSource lexer = new UMLDiagramLexer(target);
            ITokenStream tokens = new CommonTokenStream(lexer);
            UMLDiagramParser parser = new UMLDiagramParser(tokens)
            {
                BuildParseTree = true
            };
        
            //ExprParser.LiteralContext result = parser.literal();
            UMLDiagramParser.DiagramContext parsedDiagram = parser.diagram();
        
            UMLDiagramVisitorConcrete visitor = new UMLDiagramVisitorConcrete();
        
            ClassDiagramManager result = visitor.VisitDiagram(parsedDiagram) as ClassDiagramManager;
        
            return result;
        }
    }
}
