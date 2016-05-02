﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace DtoGenerator.Logic.Infrastructure.TreeProcessing
{
    public class GeneratedCodeRemover : CSharpSyntaxRewriter
    {
        public int CustomPropertiesCount { get; set; }

        public SyntaxNode FirstCustomProperty { get; set; }
        public SyntaxNode LastCustomProperty { get; set; }

        public SyntaxNode FirstCustomSelector { get; set; }
        public SyntaxNode LastCustomSelector { get; set; }

        public SyntaxNode FirstCustomMapperStatement { get; set; }
        public SyntaxNode LastCustomMapperStatement { get; set; }

        private CustomCodeLocator _finder;

        public GeneratedCodeRemover(CustomCodeLocator finder)
        {
            this.CustomPropertiesCount = 0;

            this._finder = finder;
        }

        public override SyntaxTrivia VisitTrivia(SyntaxTrivia trivia)
        {
            if (trivia.Kind() == SyntaxKind.SingleLineCommentTrivia)
            {
                var text = trivia.ToString();

                if (text.Contains("////BCPS/") || text.Contains("////ECPS/") || text.Contains("////BCSS/") || text.Contains("////ECSS/") || text.Contains("////BCMS/") || text.Contains("////ECMS/"))
                    return SyntaxFactory.Whitespace("");
            }

            return base.VisitTrivia(trivia);
        }

        public override SyntaxNode VisitPropertyDeclaration(PropertyDeclarationSyntax node)
        {
            if (!node.FirstAncestorOrSelf<ClassDeclarationSyntax>().Identifier.Text.Contains("Mapper"))
            {
                // check if node is automatically generated (not wrapped inside custom comments)
                if (this.IsNodeAutoGenerated(node, this._finder.CustomPropertyBegin, this._finder.CustomPropertyEnd))
                {
                    return null;
                }
                else
                {
                    this.CustomPropertiesCount++;

                    if (this.FirstCustomProperty == null)
                        this.FirstCustomProperty = node;

                    this.LastCustomProperty = node;
                }
            }

            return base.VisitPropertyDeclaration(node);
        }

        public override SyntaxNode VisitExpressionStatement(ExpressionStatementSyntax node)
        {
            if (node.FirstAncestorOrSelf<MethodDeclarationSyntax>() != null && node.FirstAncestorOrSelf<MethodDeclarationSyntax>().Identifier.Text == "MapToModel")
            {
                // this is mapper expression. Check if automatically generated..
                if (this.IsNodeAutoGenerated(node, this._finder.CustomMapperBegin, this._finder.CustomMapperEnd))
                {
                    return null;
                }
                else
                {
                    if (this.FirstCustomMapperStatement == null)
                        this.FirstCustomMapperStatement = node;

                    this.LastCustomMapperStatement = node;
                }
            }

            return base.VisitExpressionStatement(node);
        }

        public override SyntaxNode VisitObjectCreationExpression(ObjectCreationExpressionSyntax node)
        {
            var customExpressions = node.Initializer.Expressions
                .Where(p => !this.IsNodeAutoGenerated(p, this._finder.CustomSelectorBegin, this._finder.CustomSelectorEnd))
                .Select(p => p.WithoutTrivia().WithTrailingTrivia(SyntaxFactory.EndOfLine("\n")))
                .ToList();

            var res = node.WithInitializer(node.Initializer.WithExpressions(SyntaxFactory.SeparatedList(customExpressions)));

            this.FirstCustomSelector = res.Initializer.Expressions.FirstOrDefault();
            this.LastCustomSelector = res.Initializer.Expressions.LastOrDefault();

            return res;
        }

        private bool IsNodeAutoGenerated(SyntaxNode node, int customCodeBegin, int customCodeEnd)
        {
            return node.GetLocation().SourceSpan.Start > customCodeEnd || node.GetLocation().SourceSpan.End < customCodeBegin;
        }
    }
}