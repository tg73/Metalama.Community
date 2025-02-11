﻿// Copyright (c) SharpCrafters s.r.o. See the LICENSE.md file in the root directory of this repository root for details.

using Metalama.Compiler;
using Metalama.Framework.Engine.AspectWeavers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Linq;
using System.Threading.Tasks;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

namespace Metalama.Community.Virtuosity.Weaver
{
    [MetalamaPlugIn]
    public sealed class VirtuosityWeaver : IAspectWeaver
    {
        public Task TransformAsync( AspectWeaverContext context )
            => context.RewriteAspectTargetsAsync( new Rewriter() );

        private sealed class Rewriter : CSharpSyntaxRewriter
        {
            private static readonly SyntaxKind[] _forbiddenModifiers =
                new[] { StaticKeyword, SealedKeyword, VirtualKeyword, OverrideKeyword };

            private static readonly SyntaxKind[] _requiredModifiers =
                new[] { PublicKeyword, ProtectedKeyword, InternalKeyword };

            private static bool CanTransformType( MemberDeclarationSyntax node )
                => node switch
                {
                    InterfaceDeclarationSyntax => false,
                    ClassDeclarationSyntax => true,
                    StructDeclarationSyntax => false,
                    RecordDeclarationSyntax record when record.ClassOrStructKeyword.IsKind( StructKeyword ) => false,
                    RecordDeclarationSyntax => true,
                    _ => throw new ArgumentOutOfRangeException()
                };

            private static SyntaxTokenList ModifyModifiers( SyntaxTokenList modifiers, bool addVirtual = true )
            {
                // Remove the sealed modifier.
                var sealedToken = modifiers.FirstOrDefault( modifier => modifier.IsKind( SealedKeyword ) );

                modifiers = modifiers.Remove( sealedToken );

                // Add the virtual modifier.
                if ( addVirtual )
                {
                    if ( !_forbiddenModifiers.Any( modifier => modifiers.Any( modifier ) )
                         && _requiredModifiers.Any( modifier => modifiers.Any( modifier ) ) )
                    {
                        modifiers = modifiers.Add(
                            SyntaxFactory.Token( VirtualKeyword )
                                .WithTrailingTrivia( SyntaxFactory.ElasticSpace ) );
                    }
                }

                return modifiers;
            }

            public override SyntaxNode? VisitClassDeclaration( ClassDeclarationSyntax node )
            {
                return ((ClassDeclarationSyntax) base.VisitClassDeclaration( node )!).WithModifiers( ModifyModifiers( node.Modifiers, false ) );
            }

            public override SyntaxNode? VisitRecordDeclaration( RecordDeclarationSyntax node )
            {
                if ( CanTransformType( node ) )
                {
                    return ((RecordDeclarationSyntax) base.VisitRecordDeclaration( node )!).WithModifiers( ModifyModifiers( node.Modifiers, false ) );
                }
                else
                {
                    return base.VisitRecordDeclaration( node );
                }
            }

            public override SyntaxNode VisitMethodDeclaration( MethodDeclarationSyntax node )
            {
                var parent = (MemberDeclarationSyntax) node.Parent!;

                if ( CanTransformType( parent ) )
                {
                    return node.WithModifiers( ModifyModifiers( node.Modifiers ) );
                }
                else
                {
                    return node;
                }
            }

            public override SyntaxNode? VisitPropertyDeclaration( PropertyDeclarationSyntax node )
            {
                var parent = (MemberDeclarationSyntax) node.Parent!;

                if ( CanTransformType( parent ) )
                {
                    return node.WithModifiers( ModifyModifiers( node.Modifiers ) );
                }
                else
                {
                    return node;
                }
            }
        }
    }
}