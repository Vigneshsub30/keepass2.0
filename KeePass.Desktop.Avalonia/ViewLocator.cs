using System;

using Avalonia.Controls;
using Avalonia.Controls.Templates;

using CommunityToolkit.Mvvm.ComponentModel;

namespace KeePass.Desktop.Avalonia
{
	/// <summary>
	/// Resolves Avalonia views from view-model types by naming convention.
	/// A ViewModel named <c>FooViewModel</c> in <c>KeePass.Core.ViewModels</c>
	/// is mapped to a View named <c>FooView</c> in
	/// <c>KeePass.Desktop.Avalonia.Views</c>.
	/// </summary>
	public sealed class ViewLocator : IDataTemplate
	{
		private static readonly string ViewModelSuffix = "ViewModel";
		private static readonly string ViewModelNamespace = "KeePass.Core.ViewModels";
		private static readonly string ViewNamespace = "KeePass.Desktop.Avalonia.Views";

		public Control? Build(object? data)
		{
			if (data is null) return null;

			string viewName = ResolveViewTypeName(data.GetType());
			if (viewName is null) return new TextBlock { Text = "View not found" };

			var type = Type.GetType(viewName);
			if (type is null)
				return new TextBlock { Text = $"View not found: {viewName}" };

			var instance = Activator.CreateInstance(type);
			return instance as Control
				?? new TextBlock { Text = $"Not a Control: {viewName}" };
		}

		public bool Match(object? data) => data is ObservableObject;

		/// <summary>
		/// Derives the fully-qualified View type name from a ViewModel type.
		/// </summary>
		public static string ResolveViewTypeName(Type viewModelType)
		{
			if (viewModelType is null) throw new ArgumentNullException(nameof(viewModelType));

			string name = viewModelType.Name;
			if (!name.EndsWith(ViewModelSuffix, StringComparison.Ordinal))
				return string.Empty;

			string baseName = name.Substring(0, name.Length - ViewModelSuffix.Length);
			string assemblyName = typeof(App).Assembly.GetName().Name;
			return $"{ViewNamespace}.{baseName}View, {assemblyName}";
		}
	}
}
