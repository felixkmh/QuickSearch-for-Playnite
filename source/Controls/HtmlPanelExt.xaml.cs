using Playnite.SDK;
using System;
using System.Collections.Generic;
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
using TheArtOfDev.HtmlRenderer.WPF;

namespace QuickSearch.Controls
{
    /// <summary>
    /// Interaktionslogik für HtmlPanelExt.xaml
    /// </summary>
    public partial class HtmlPanelExt : HtmlPanel
    {
        public HtmlPanelExt()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty InnerHtmlProperty =
            DependencyProperty.Register(nameof(InnerHtml), typeof(String), typeof(HtmlPanelExt), new PropertyMetadata(string.Empty, OnContentChanged));

        public String InnerHtml { get => (string)GetValue(InnerHtmlProperty); set => SetValue(InnerHtmlProperty, value); }

        private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (e.Property == InnerHtmlProperty && d is HtmlPanelExt panel)
            {
                var html = e.NewValue as string;
                if (string.IsNullOrEmpty(html))
                {
                    panel.Text = null;
                } else
                {
                    panel.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.DataBind, (Action)delegate { 
                        var textColor = ResourceProvider.GetResource<Color?>("TextColor") ?? Colors.White;

                        if (!html.Contains("<html>"))
                        {
                            html = template.Replace("{text}", html).Replace("{foreground}", textColor.ToHtml());
                        }
                        panel.Text = html;
                    });
                }
            }
        }

        private const string template = @"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""UTF-8"">
    <style type=""text/css"">
        HTML,BODY
        {
            color: {foreground};
            margin: 0;
            padding: 0;
        }

        a {
            text-decoration: none;
        }

        img {
            max-width: 100%;
        }
    </style>
    <title>Game Description</title>
</head>
<body>
<div>
{text}
</div>
</body>
</html>";
    }
}
