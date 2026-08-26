using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace LibraryManager.Api.ModelBinding;

[AttributeUsage(AttributeTargets.Parameter)]
public sealed class FromIdempotencyKeyAttribute : ModelBinderAttribute
{
    public FromIdempotencyKeyAttribute()
        : base(typeof(IdempotencyKeyModelBinder))
    {
        BindingSource = BindingSource.Header;
    }
}
