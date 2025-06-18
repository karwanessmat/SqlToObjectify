using System.Dynamic;
using System.Reflection;

namespace SqlToObjectify
{
    public static class ObjectMapper
    {
        public static T? MapToObject<T>(this object? result)
        {
            if (result is not ExpandoObject obj)
            {
                return default(T);
            }

            //var model = new T();
            var model = Activator.CreateInstance<T>();
            var type = typeof(T);
            SetProperties(obj, type, model);

            return model;
        }

        public static List<T>? MapToObjectList<T>(this object? result)
        {
            if (result is not List<ExpandoObject> listObjList)
            {
                return default;
            }
            var modelList = new List<T>();
            var type = typeof(T);

            foreach (var listObj in listObjList)
            {
                var listItem = Activator.CreateInstance<T>();
                SetProperties(listObj, type, listItem);
                modelList.Add(listItem);
            }
            return modelList;
        }
       
        private static void SetProperties<T>(ExpandoObject obj, IReflect type, T model)
        {
            foreach (var (key, value) in obj)
            {
                var property = type.GetProperty(key, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
                if (property == null) continue;

                var propertyType = property.PropertyType;

                if (value == null || value is DBNull)
                {
                    if (!propertyType.IsValueType || Nullable.GetUnderlyingType(propertyType) != null)
                    {
                        property.SetValue(model, null);
                    }
                    else
                    {
                        continue; // skip non-nullable types
                    }
                }
                else
                {
                    var targetType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

                    try
                    {
                        // Handle enums (both string and int representation)
                        if (targetType.IsEnum)
                        {
                            if (value is string s)
                                property.SetValue(model, Enum.Parse(targetType, s, ignoreCase: true));
                            else
                                property.SetValue(model, Enum.ToObject(targetType, value));
                        }
                        else
                        {
                            property.SetValue(model, Convert.ChangeType(value, targetType));
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new InvalidOperationException($"Failed to set property '{property.Name}' with value '{value}'", ex);
                    }
                }
            }
        }

    }

}
