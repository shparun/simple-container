using System;
using System.Collections;
using System.IO;
using System.Reflection;
using SimpleContainer.Helpers;

namespace SimpleContainer.Implementation
{
	internal class ServiceDependency
	{
		public ContainerService ContainerService { get; private set; }
		public object Value { get; private set; }
		public string Name { get; private set; }
		public string Comment { get; set; }
		public ServiceStatus Status { get; private set; }

		private ServiceDependency()
		{
		}

		public ServiceDependency CastTo(Type targetType)
		{
			object castedValue;
			if (!TryCast(Value, targetType, out castedValue))
				return Error(ContainerService, Name, "can't cast value [{0}] from [{1}] to [{2}] for dependency [{3}]",
					Value, Value.GetType().FormatName(), targetType.FormatName(), Name);
			return ReferenceEquals(castedValue, Value) ? this : CloneWithValue(castedValue);
		}

		private static bool TryCast(object source, Type targetType, out object value)
		{
			if (source == null || targetType.IsInstanceOfType(source))
			{
				value = source;
				return true;
			}
			var underlyingType = Nullable.GetUnderlyingType(targetType);
			if (underlyingType != null)
				targetType = underlyingType;
			if (source is int && targetType == typeof(long))
			{
				value = (long)(int)source;
				return true;
			}
			value = null;
			return false;
		}

		private ServiceDependency CloneWithValue(object value)
		{
			return new ServiceDependency
			{
				ContainerService = ContainerService,
				Comment = Comment,
				Name = Name,
				Status = Status,
				resourceName = resourceName,
				constantKind = constantKind,
				Value = value
			};
		}

		public static ServiceDependency Constant(ParameterInfo formalParameter, object value)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.Ok,
				Value = value,
				constantKind = ConstantKind.Value
			}.WithName(formalParameter, null);
		}

		public static ServiceDependency Resource(ParameterInfo formalParameter, string resourceName, Stream value)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.Ok,
				constantKind = ConstantKind.Resource,
				resourceName = resourceName,
				Value = value
			}.WithName(null, formalParameter.Name);
		}

		public static ServiceDependency Service(ContainerService service, object value, string name = null)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.Ok,
				Value = value,
				ContainerService = service
			}.WithName(null, name);
		}

		public static ServiceDependency NotResolved(ContainerService service, string name = null)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.NotResolved,
				ContainerService = service
			}.WithName(null, name);
		}

		public static ServiceDependency Error(ContainerService containerService, string name, string message,
			params object[] args)
		{
			return new ServiceDependency
			{
				ContainerService = containerService,
				Status = ServiceStatus.Error,
				Comment = string.Format(message, args)
			}.WithName(null, name);
		}

		public static ServiceDependency ServiceError(ContainerService service, string name = null)
		{
			return new ServiceDependency
			{
				Status = ServiceStatus.DependencyError,
				ContainerService = service
			}.WithName(null, name);
		}

		public void WriteConstructionLog(ConstructionLogContext context)
		{
			context.WriteIndent();
			if (ContainerService != null)
			{
				context.UsedFromDependency = this;
				ContainerService.WriteConstructionLog(context);
				return;
			}
			if (Status != ServiceStatus.Ok)
				context.Writer.WriteMeta("!");
			context.Writer.WriteName(Name);
			if (Comment != null && Status != ServiceStatus.Error)
			{
				context.Writer.WriteMeta(" - ");
				context.Writer.WriteMeta(Comment);
			}
			if (Status == ServiceStatus.Ok && constantKind.HasValue)
			{
				if (constantKind == ConstantKind.Value)
				{
					if (Value == null || Value.GetType().IsSimpleType())
						context.Writer.WriteMeta(" -> " + InternalHelpers.DumpValue(Value));
					else
					{
						context.Writer.WriteMeta(" const");
						WriteValue(context);
					}
				}
				if (constantKind == ConstantKind.Resource)
					context.Writer.WriteMeta(string.Format(" resource [{0}]", resourceName));
			}
			if (Status == ServiceStatus.Error)
				context.Writer.WriteMeta(" <---------------");
			context.Writer.WriteNewLine();
		}

		private void WriteValue(ConstructionLogContext context)
		{
			string formattedValue;

			if (TryFormat(Value, context, out formattedValue))
			{
				context.Writer.WriteMeta(" -> " + formattedValue);
				return;
			}
			var enumerable = Value as IEnumerable;
			if (enumerable != null)
			{
				context.Indent++;
				foreach (var item in enumerable)
				{
					if (!TryFormat(item, context, out formattedValue))
						formattedValue = "?";
					context.Writer.WriteNewLine();
					context.WriteIndent();
					context.Writer.WriteMeta(formattedValue);
				}
				context.Indent--;
				return;
			}
			context.Indent++;
			foreach (var prop in Value.GetType().GetProperties())
			{
				if (!prop.CanRead)
					continue;
				var propVal = prop.GetValue(Value, null);
				if (!TryFormat(propVal, context, out formattedValue))
					continue;
				context.Writer.WriteNewLine();
				context.WriteIndent();
				context.Writer.WriteName(prop.Name);
				context.Writer.WriteMeta(" -> " + formattedValue);
			}
			context.Indent--;
		}

		private static bool TryFormat(object value, ConstructionLogContext context, out string formattedValue)
		{
			Func<object, string> formatter;
			if (value != null && context.ValueFormatters.TryGetValue(value.GetType(), out formatter))
			{
				formattedValue = formatter(value);
				return true;
			}
			if (value == null || value.GetType().IsSimpleType())
			{
				formattedValue = InternalHelpers.DumpValue(value);
				return true;
			}
			formattedValue = null;
			return false;
		}

		private ServiceDependency WithName(ParameterInfo parameter, string name)
		{
			Name = BuildName(parameter, name);
			return this;
		}

		private string BuildName(ParameterInfo parameter, string name)
		{
			if (name != null)
				return name;
			var type = GetDependencyType();
			return type == null || type.IsSimpleType() || type.UnwrapEnumerable() != type ? parameter.Name : type.FormatName();
		}

		private Type GetDependencyType()
		{
			if (ContainerService != null)
				return ContainerService.Type;
			if (Value != null)
				return Value.GetType();
			return null;
		}

		private ConstantKind? constantKind;
		private string resourceName;

		private enum ConstantKind
		{
			Value,
			Resource
		}
	}
}