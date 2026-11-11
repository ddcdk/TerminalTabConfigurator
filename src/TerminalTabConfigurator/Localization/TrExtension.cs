using System.Windows;
using System.Windows.Markup;

namespace TerminalTabConfigurator.Localization;

/// <summary>
/// <c>{l:Tr Key}</c>: a translated text that follows <see cref="Loc.Language"/>.
/// On a dependency property it is a binding; elsewhere it is the current text.
/// </summary>
[MarkupExtensionReturnType(typeof(object))]
public sealed class TrExtension(string key) : MarkupExtension
{
    [ConstructorArgument("key")]
    public string Key { get; set; } = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var target = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
        if (target?.TargetProperty is DependencyProperty || target?.TargetObject is Setter)
            return Loc.Bind(Key).ProvideValue(serviceProvider);
        return Loc.T(Key);
    }
}
