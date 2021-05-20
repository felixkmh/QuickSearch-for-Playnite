using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace QuickSearch.Controls
{
    /// <summary>
    /// Interaktionslogik für ActionButton.xaml
    /// </summary>
    public partial class ActionButton : Button
    {
        public Boolean IsSelected 
        {
            get => (Boolean)GetValue(IsSelectedProperty); 
            set => SetValue(IsSelectedProperty, value);
        }

        public static DependencyProperty IsSelectedProperty =
            DependencyProperty.Register(nameof(IsSelected), typeof(Boolean), typeof(ActionButton), new PropertyMetadata(false));

        public ActionButton()
        {
            InitializeComponent();
            if (Style is Style)
            {
                var style = new Style(typeof(Button), Style);
                var mouseOverTriggers = Style.Triggers.OfType<Trigger>().Where(t => t.Property.Name == "IsMouseOver");
                foreach (var trigger in mouseOverTriggers)
                {
                    var selectedTrigger = new Trigger()
                    {
                        Property = IsSelectedProperty,
                        Value = trigger.Value
                    };
                    foreach (var setter in trigger.Setters)
                    {
                        selectedTrigger.Setters.Add(setter);
                    }
                    foreach (var action in trigger.EnterActions)
                    {
                        selectedTrigger.EnterActions.Add(action);
                    }
                    foreach (var action in trigger.ExitActions)
                    {
                        selectedTrigger.ExitActions.Add(action);
                    }
                    style.Triggers.Add(selectedTrigger);
                }

                var enabledTriggers = Style.Triggers.OfType<Trigger>().Where(t => t.Property.Name == "IsEnabled");
                foreach (var trigger in enabledTriggers)
                {
                    var selectedTrigger = new Trigger()
                    {
                        Property = IsSelectedProperty,
                        Value = !(Boolean)trigger.Value
                    };
                    foreach (var setter in trigger.Setters)
                    {
                        selectedTrigger.Setters.Add(setter);
                    }
                    foreach (var action in trigger.EnterActions)
                    {
                        selectedTrigger.EnterActions.Add(action);
                    }
                    foreach (var action in trigger.ExitActions)
                    {
                        selectedTrigger.ExitActions.Add(action);
                    }
                    style.Triggers.Add(selectedTrigger);
                }
                Style = style;
            }
        }
    }
}
