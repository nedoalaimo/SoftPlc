using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;
using System.Runtime.Serialization;

namespace SoftPlc.Services
{
    public class EnumSchemaFilter : ISchemaFilter
    {
        public void Apply(OpenApiSchema schema, SchemaFilterContext context)
        {
            if (context.Type.IsEnum)
            {
                schema.Enum.Clear();
                foreach (var enumName in Enum.GetNames(context.Type))
                {
                    var memberInfo = context.Type.GetMember(enumName)[0];
                    var enumMemberAttribute = memberInfo.GetCustomAttributes(typeof(EnumMemberAttribute), false)
                        .OfType<EnumMemberAttribute>()
                        .FirstOrDefault();

                    var enumValue = enumMemberAttribute?.Value ?? enumName.ToLower();
                    schema.Enum.Add(new Microsoft.OpenApi.Any.OpenApiString(enumValue));
                }
            }
        }
    }
}
