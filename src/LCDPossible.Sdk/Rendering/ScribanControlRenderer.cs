using LCDPossible.Sdk.Controls;
using Scriban;
using Scriban.Runtime;

namespace LCDPossible.Sdk.Rendering;

/// <summary>
/// Renders controls using Scriban templates from theme configuration.
/// Allows themes to provide custom HTML templates for any control type.
/// </summary>
public class ScribanControlRenderer : IControlRenderer
{
    private readonly Template _template;

    /// <summary>
    /// The control type this renderer handles.
    /// </summary>
    public string ControlType { get; }

    /// <summary>
    /// Creates a renderer from a Scriban template string.
    /// </summary>
    /// <param name="controlType">The control type identifier.</param>
    /// <param name="templateContent">Scriban template content.</param>
    /// <exception cref="FormatException">If the template has syntax errors.</exception>
    public ScribanControlRenderer(string controlType, string templateContent)
    {
        ControlType = controlType;
        _template = Template.Parse(templateContent);

        if (_template.HasErrors)
        {
            var errors = string.Join("; ", _template.Messages.Select(m => m.Message));
            throw new FormatException($"Template parse error for '{controlType}': {errors}");
        }
    }

    /// <summary>
    /// Renders the control using the Scriban template.
    /// </summary>
    public string Render(SemanticControl control, ControlRenderContext context)
    {
        var scriptObject = new ScriptObject();

        // Import all control properties as lowercase variables
        ImportControlProperties(scriptObject, control);

        // Add context values
        scriptObject["colors"] = context.Colors;
        scriptObject["assets_path"] = context.AssetsPath;
        scriptObject["grid_columns"] = context.GridColumns;
        scriptObject["grid_rows"] = context.GridRows;

        // Add helper functions
        scriptObject.Import("get_status_class", new Func<StatusLevel, string>(GetStatusClass));
        scriptObject.Import("get_size_class", new Func<ValueSize, string>(GetSizeClass));
        scriptObject.Import("calc_percent", new Func<double, double, double>(CalcPercent));

        var templateContext = new TemplateContext();
        templateContext.MemberRenamer = member => ToSnakeCase(member.Name);
        templateContext.PushGlobal(scriptObject);

        return _template.Render(templateContext);
    }

    private static void ImportControlProperties(ScriptObject scriptObject, SemanticControl control)
    {
        // Import base properties
        scriptObject["col_span"] = control.ColSpan;
        scriptObject["row_span"] = control.RowSpan;
        scriptObject["control_type"] = control.ControlType;

        // Import type-specific properties using reflection
        foreach (var prop in control.GetType().GetProperties())
        {
            var name = ToSnakeCase(prop.Name);
            var value = prop.GetValue(control);

            // Convert enums to their string representation
            if (value is Enum enumValue)
                scriptObject[name] = enumValue.ToString().ToLowerInvariant();
            else
                scriptObject[name] = value;
        }
    }

    private static string GetStatusClass(StatusLevel status) => status switch
    {
        StatusLevel.Info => "text-info",
        StatusLevel.Success => "text-success",
        StatusLevel.Warning => "text-warning",
        StatusLevel.Error => "text-error",
        _ => "text-primary"
    };

    private static string GetSizeClass(ValueSize size) => size switch
    {
        ValueSize.Small => "text-xl",
        ValueSize.Medium => "text-3xl",
        ValueSize.Large => "text-5xl",
        ValueSize.XLarge => "text-6xl",
        ValueSize.Hero => "text-7xl",
        _ => "text-3xl"
    };

    private static double CalcPercent(double value, double max) =>
        max > 0 ? Math.Min(100, Math.Max(0, (value / max) * 100)) : 0;

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;

        var result = new System.Text.StringBuilder();
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0) result.Append('_');
                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }
        return result.ToString();
    }
}
