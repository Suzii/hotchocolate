using System;
using System.Threading;
using System.Threading.Tasks;
using HotChocolate.Language;
using HotChocolate.Types;
using StrawberryShake.Generators.Descriptors;
using StrawberryShake.Generators.Utilities;
using static StrawberryShake.Generators.Utilities.NameUtils;

namespace StrawberryShake.Generators.CSharp
{
    public class InterfaceGenerator
        : CodeGenerator<IInterfaceDescriptor>
    {
        protected override async Task WriteAsync(
            CodeWriter writer,
            IInterfaceDescriptor descriptor,
            ITypeLookup typeLookup)
        {
            await writer.WriteIndentAsync();
            await writer.WriteAsync("public interface ");
            await writer.WriteAsync(descriptor.Name);
            await writer.WriteLineAsync();

            using (writer.IncreaseIndent())
            {
                for (int i = 0; i < descriptor.Implements.Count; i++)
                {
                    await writer.WriteIndentAsync();

                    if (i == 0)
                    {
                        await writer.WriteAsync(':');
                    }
                    else
                    {
                        await writer.WriteAsync(',');
                    }

                    await writer.WriteSpaceAsync();
                    await writer.WriteAsync(descriptor.Implements[i].Name);
                    await writer.WriteLineAsync();
                }
            }

            await writer.WriteIndentAsync();
            await writer.WriteAsync("{");
            await writer.WriteLineAsync();

            using (writer.IncreaseIndent())
            {
                if (descriptor.Type is IComplexOutputType complexType)
                {
                    for (int i = 0; i < descriptor.Fields.Count; i++)
                    {
                        IFieldDescriptor fieldDescriptor = descriptor.Fields[i];

                        if (complexType.Fields.ContainsField(
                            fieldDescriptor.Selection.Name.Value))
                        {
                            string typeName = typeLookup.GetTypeName(
                                fieldDescriptor.Selection,
                                fieldDescriptor.Type,
                                true);

                            string propertyName = GetPropertyName(fieldDescriptor.ResponseName);

                            if (i > 0)
                            {
                                await writer.WriteLineAsync();
                            }

                            await writer.WriteIndentAsync();
                            await writer.WriteAsync(typeName);
                            await writer.WriteSpaceAsync();
                            await writer.WriteAsync(propertyName);
                            await writer.WriteSpaceAsync();
                            await writer.WriteAsync("{ get; }");
                            await writer.WriteLineAsync();
                        }
                        else
                        {
                            // TODO : exception
                            // TODO : resources
                            throw new Exception("Unknown field.");
                        }
                    }
                }
            }

            await writer.WriteIndentAsync();
            await writer.WriteAsync("}");
            await writer.WriteLineAsync();
        }
    }

    public class ClientInterfaceGenerator
        : CodeGenerator<IClientDescriptor>
    {
        protected override async Task WriteAsync(
            CodeWriter writer,
            IClientDescriptor descriptor,
            ITypeLookup typeLookup)
        {
            await writer.WriteIndentAsync();
            await writer.WriteAsync("public interface ");
            await writer.WriteAsync(GetInterfaceName(descriptor.Name));
            await writer.WriteLineAsync();

            await writer.WriteIndentAsync();
            await writer.WriteAsync("{");
            await writer.WriteLineAsync();

            using (writer.IncreaseIndent())
            {
                for (int i = 0; i < descriptor.Operations.Count; i++)
                {
                    IOperationDescriptor operation = descriptor.Operations[i];

                    string typeName = typeLookup.GetTypeName(
                        operation.OperationType,
                        operation.ResultType.Name,
                        true);

                    if (i > 0)
                    {
                        await writer.WriteLineAsync();
                    }

                    await WriteOperationAsync(
                        writer, operation, typeName, false, typeLookup);
                    await WriteOperationAsync(
                        writer, operation, typeName, true, typeLookup);
                }
            }

            await writer.WriteIndentAsync();
            await writer.WriteAsync("}");
            await writer.WriteLineAsync();
        }

        private async Task WriteOperationAsync(
            CodeWriter writer,
            IOperationDescriptor operation,
            string operationTypeName,
            bool cancellationToken,
            ITypeLookup typeLookup)
        {
            await writer.WriteIndentAsync();
            if (operation.Operation.Operation == OperationType.Subscription)
            {
                await writer.WriteAsync(
                    $"Task<IResponseStream<{operationTypeName}>> ");
            }
            else
            {
                await writer.WriteAsync(
                    $"Task<IOperationResult<{operationTypeName}>> ");
            }
            await writer.WriteAsync(
                $"{operation.Operation.Name.Value}Async(");

            using (writer.IncreaseIndent())
            {
                for (int j = 0; j < operation.Arguments.Count; j++)
                {
                    Descriptors.IArgumentDescriptor argument =
                        operation.Arguments[j];

                    if (j > 0)
                    {
                        await writer.WriteAsync(',');
                    }

                    await writer.WriteLineAsync();

                    string argumentType = typeLookup.GetTypeName(
                        argument.Type,
                        argument.Type.NamedType().Name,
                        true);

                    await writer.WriteIndentAsync();
                    await writer.WriteAsync(argumentType);
                    await writer.WriteSpaceAsync();
                    await writer.WriteAsync(GetFieldName(argument.Name));
                }

                if (cancellationToken)
                {
                    if (operation.Arguments.Count > 0)
                    {
                        await writer.WriteAsync(',');
                    }

                    await writer.WriteLineAsync();
                    await writer.WriteIndentAsync();
                    await writer.WriteAsync("CancellationToken cancellationToken");
                }

                await writer.WriteAsync(')');
                await writer.WriteAsync(';');
                await writer.WriteLineAsync();
            }
        }

    }
}
