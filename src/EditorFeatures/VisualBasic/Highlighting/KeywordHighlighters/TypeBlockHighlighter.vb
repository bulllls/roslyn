﻿' Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

Imports System.ComponentModel.Composition
Imports System.Threading
Imports Microsoft.CodeAnalysis.Editor.Implementation.Highlighting
Imports Microsoft.CodeAnalysis.Text
Imports Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace Microsoft.CodeAnalysis.Editor.VisualBasic.KeywordHighlighting
    <ExportHighlighter(LanguageNames.VisualBasic)>
    Friend Class TypeBlockHighlighter
        Inherits AbstractKeywordHighlighter(Of SyntaxNode)

        <ImportingConstructor>
        Public Sub New()
        End Sub

        Protected Overloads Overrides Sub AddHighlights(node As SyntaxNode, highlights As List(Of TextSpan), cancellationToken As CancellationToken)
            Dim endBlockStatement = TryCast(node, EndBlockStatementSyntax)
            If endBlockStatement IsNot Nothing Then
                If Not endBlockStatement.IsKind(SyntaxKind.EndClassStatement,
                                                SyntaxKind.EndInterfaceStatement,
                                                SyntaxKind.EndModuleStatement,
                                                SyntaxKind.EndStructureStatement) Then
                    Return
                End If
            End If

            Dim typeBlock = node.GetAncestor(Of TypeBlockSyntax)()
            If typeBlock Is Nothing Then
                Return
            End If

            With typeBlock
                With .BlockStatement
                    Dim firstKeyword = If(.Modifiers.Count > 0, .Modifiers.First(), .DeclarationKeyword)
                    highlights.Add(TextSpan.FromBounds(firstKeyword.SpanStart, .DeclarationKeyword.Span.End))
                End With

                highlights.Add(.EndBlockStatement.Span)
            End With
        End Sub
    End Class
End Namespace
