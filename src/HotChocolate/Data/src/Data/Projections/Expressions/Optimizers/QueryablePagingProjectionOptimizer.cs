using System;
using System.Collections.Generic;
using System.Linq;
using HotChocolate.Execution.Processing;
using HotChocolate.Language;
using HotChocolate.Resolvers;
using HotChocolate.Types;
using HotChocolate.Types.Pagination;
using static HotChocolate.Data.Projections.WellKnownProjectionFields;

namespace HotChocolate.Data.Projections.Handlers;

public class QueryablePagingProjectionOptimizer : IProjectionOptimizer
{
    public bool CanHandle(ISelection field) =>
        field.DeclaringType is IPageType &&
        field.Field.Name.Value is "edges" or "items" or "nodes";

    public Selection RewriteSelection(
        SelectionOptimizerContext context,
        Selection selection)
    {
        if (context.Type.NamedType() is not IPageType pageType)
        {
            throw ThrowHelper
                .PagingProjectionOptimizer_NotAPagingField(
                    selection.DeclaringType,
                    selection.Field);
        }

        IReadOnlyList<ISelectionNode> selections = CollectSelection(context);

        context.Fields[CombinedEdgeField] =
            CreateCombinedSelection(context,
                selection,
                selection.DeclaringType,
                pageType,
                selections);

        return selection;
    }

    private Selection CreateCombinedSelection(
        SelectionOptimizerContext context,
        ISelection selection,
        IObjectType declaringType,
        IPageType pageType,
        IReadOnlyList<ISelectionNode> selections)
    {
        (var fieldName, IObjectField? nodesField) = TryGetObjectField(pageType);

        var combinedField = new FieldNode(
            null,
            new NameNode(fieldName),
            new NameNode(CombinedEdgeField),
            null,
            Array.Empty<DirectiveNode>(),
            Array.Empty<ArgumentNode>(),
            new SelectionSetNode(selections));

        FieldDelegate nodesPipeline =
            selection.ResolverPipeline ??
            context.CompileResolverPipeline(nodesField, combinedField);

        return new Selection(
            context.GetNextId(),
            declaringType,
            nodesField,
            combinedField,
            nodesPipeline,
            arguments: selection.Arguments,
            internalSelection: true);
    }

    private static (string filedName, IObjectField field) TryGetObjectField(IPageType type)
    {
        if (type.Fields.FirstOrDefault(x => x.Name.Value == "nodes") is { } nodes)
        {
            return ("nodes", nodes);
        }

        if (type.Fields.FirstOrDefault(x => x.Name.Value == "items") is { } items)
        {
            return ("items", items);
        }

        throw new GraphQLException(
            ErrorHelper.ProjectionVisitor_NodeFieldWasNotFound(type));
    }

    private IReadOnlyList<ISelectionNode> CollectSelection(SelectionOptimizerContext context)
    {
        var selections = new List<ISelectionNode>();

        CollectSelectionOfNodes(context, selections);
        CollectSelectionOfItems(context, selections);
        CollectSelectionOfEdges(context, selections);

        return selections;
    }

    private static void CollectSelectionOfEdges(
        SelectionOptimizerContext context,
        List<ISelectionNode> selections)
    {
        if (context.Fields.Values
            .FirstOrDefault(x => x.Field.Name == "edges") is { } edgeSelection)
        {
            foreach (ISelectionNode? edgeSubField in edgeSelection.SelectionSet!.Selections)
            {
                if (edgeSubField is FieldNode edgeSubFieldNode &&
                    edgeSubFieldNode.Name.Value is "node" &&
                    edgeSubFieldNode.SelectionSet?.Selections is not null)
                {
                    foreach (ISelectionNode? nodeField in edgeSubFieldNode.SelectionSet.Selections)
                    {
                        selections.Add(
                            CloneSelectionSetVisitor.Default.CloneSelectionNode(nodeField));
                    }
                }
            }
        }
    }

    private static void CollectSelectionOfItems(
        SelectionOptimizerContext context,
        List<ISelectionNode> selections)
    {
        if (context.Fields.Values
            .FirstOrDefault(x => x.Field.Name == "items") is { } itemSelection)
        {
            foreach (ISelectionNode? nodeField in itemSelection.SelectionSet!.Selections)
            {
                selections.Add(CloneSelectionSetVisitor.Default.CloneSelectionNode(nodeField));
            }
        }
    }

    private static void CollectSelectionOfNodes(
        SelectionOptimizerContext context,
        List<ISelectionNode> selections)
    {
        if (context.Fields.Values
            .FirstOrDefault(x => x.Field.Name == "nodes") is { } nodeSelection)
        {
            foreach (ISelectionNode? nodeField in nodeSelection.SelectionSet!.Selections)
            {
                selections.Add(CloneSelectionSetVisitor.Default.CloneSelectionNode(nodeField));
            }
        }
    }

    private sealed class CloneSelectionSetVisitor : QuerySyntaxRewriter<object>
    {
        private static readonly object _context = new();

        protected override SelectionSetNode RewriteSelectionSet(
            SelectionSetNode node,
            object context)
        {
            return new(base.RewriteSelectionSet(node, context).Selections);
        }

        public ISelectionNode CloneSelectionNode(ISelectionNode selection)
        {
            return RewriteSelection(selection, _context);
        }

        public static readonly CloneSelectionSetVisitor Default = new();
    }
}
