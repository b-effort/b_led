using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;

namespace System.Text.Json.Serialization.Metadata; 

sealed class DataContractJsonTypeInfoResolver : DefaultJsonTypeInfoResolver {
	public JsonNamingPolicy? PropertyNamingPolicy { get; init; }
	
	static bool IsNullOrDefault(object? obj) {
		if (obj is null) {
			return true;
		}

		Type type = obj.GetType();

		if (!type.IsValueType) {
			return false;
		}

		return RuntimeHelpers.GetUninitializedObject(type).Equals(obj);
	}

	static IEnumerable<MemberInfo> EnumerateFieldsAndProperties(Type type, BindingFlags bindingFlags) {
		foreach (FieldInfo fieldInfo in type.GetFields(bindingFlags)) {
			yield return fieldInfo;
		}

		foreach (PropertyInfo propertyInfo in type.GetProperties(bindingFlags)) {
			yield return propertyInfo;
		}
	}

	IEnumerable<JsonPropertyInfo> CreateDataMembers(JsonTypeInfo jsonTypeInfo) {
		bool isDataContract = jsonTypeInfo.Type.GetCustomAttribute<DataContractAttribute>() != null;
		BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.Public;

		if (isDataContract) {
			bindingFlags |= BindingFlags.NonPublic;
		}

		foreach (MemberInfo memberInfo in EnumerateFieldsAndProperties(jsonTypeInfo.Type, bindingFlags)) {
			DataMemberAttribute? attr = null;
			if (isDataContract) {
				attr = memberInfo.GetCustomAttribute<DataMemberAttribute>();
				if (attr == null) {
					continue;
				}
			} else {
				if (memberInfo.GetCustomAttribute<IgnoreDataMemberAttribute>() != null) {
					continue;
				}
			}

			Func<object, object?>? getValue = null;
			Action<object, object?>? setValue = null;
			Type? propertyType;
			string? propertyName;

			if (memberInfo.MemberType == MemberTypes.Field && memberInfo is FieldInfo fieldInfo) {
				propertyName = attr?.Name ?? fieldInfo.Name;
				propertyType = fieldInfo.FieldType;
				getValue = fieldInfo.GetValue;
				setValue = (obj, value) => fieldInfo.SetValue(obj, value);
			} else if (memberInfo.MemberType == MemberTypes.Property && memberInfo is PropertyInfo propertyInfo) {
				propertyName = attr?.Name ?? propertyInfo.Name;
				propertyType = propertyInfo.PropertyType;
				if (propertyInfo.CanRead) {
					getValue = propertyInfo.GetValue;
				}
				if (propertyInfo.CanWrite) {
					setValue = (obj, value) => propertyInfo.SetValue(obj, value);
				}
			} else {
				continue;
			}
			
			if (this.PropertyNamingPolicy != null)
				propertyName = this.PropertyNamingPolicy.ConvertName(propertyName);

			JsonPropertyInfo jsonPropertyInfo = jsonTypeInfo.CreateJsonPropertyInfo(propertyType, propertyName);

			jsonPropertyInfo.Get = getValue;
			jsonPropertyInfo.Set = setValue;

			if (attr != null) {
				jsonPropertyInfo.Order = attr.Order;
				jsonPropertyInfo.ShouldSerialize = !attr.EmitDefaultValue
					? (_, obj) => !IsNullOrDefault(obj)
					: null;
			}

			yield return jsonPropertyInfo;
		}
	}

	public override JsonTypeInfo GetTypeInfo(Type type, JsonSerializerOptions options) {
		JsonTypeInfo jsonTypeInfo = base.GetTypeInfo(type, options);

		if (jsonTypeInfo.Kind != JsonTypeInfoKind.Object)
			return jsonTypeInfo;

		jsonTypeInfo.Properties.Clear();
		if (jsonTypeInfo.Kind == JsonTypeInfoKind.Object) {
			foreach (var jsonPropertyInfo in this.CreateDataMembers(jsonTypeInfo).OrderBy(x => x.Order)) {
				jsonTypeInfo.Properties.Add(jsonPropertyInfo);
			}
		}

		return jsonTypeInfo;
	}
}
