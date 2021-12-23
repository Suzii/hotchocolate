using System;
using HotChocolate.Types.Descriptors;

namespace HotChocolate.Types;

/// <summary>
/// The `@oneOf` directive is used within the type system definition language
/// to indicate an Input Object is a Oneof Input Object.
/// </summary>
public sealed class OneOfAttribute : InputObjectTypeDescriptorAttribute
{
    public override void OnConfigure(
        IDescriptorContext context,
        IInputObjectTypeDescriptor descriptor,
        Type type)
        => descriptor.OneOf();
}
