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
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace QuickSearch.Controls
{
    /// <summary>
    /// Interaktionslogik für AutoScrollViewer.xaml
    /// </summary>
    public partial class AutoScrollViewer : AnimatedScrollViewer
    {

        public bool IsAnimating
        {
            get => (bool)GetValue(IsAnimatingProperty);
            set => SetValue(IsAnimatingProperty, value);
        }

        public double AnimationSpeed { get; set; } = 25;

        private Storyboard storyboard;

        private static void OnIsAnimatingChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is AutoScrollViewer asv)
            {
                bool newValue = (bool)e.NewValue;

                if (newValue)
                {
                    asv.StartAnimation();
                }
                else
                {
                    asv.ResetAnimation();
                }

                asv.IsAnimating = newValue;
            }
        }

        private void ResetAnimation()
        {
            if (storyboard is Storyboard)
            {
                storyboard.Stop(this);
                storyboard.Remove(this);
            }

            Opacity = 1;
            HorizontalOffset = 0;
            VerticalOffset = 0;
        }

        private void StartAnimation()
        {
            ResetAnimation();

            if (ScrollableHeight <= 0 && ScrollableWidth <= 0)
            {
                return;
            }

            TimeSpan horitzontalDuration = TimeSpan.FromSeconds(ScrollableWidth / AnimationSpeed);
            TimeSpan delay = TimeSpan.FromSeconds(2);
            var horizontalAnimation = new DoubleAnimation
            {
                From = 0,
                To = ScrollableWidth,
                Duration = horitzontalDuration,
                BeginTime = delay,
            };

            TimeSpan verticalDuration = TimeSpan.FromSeconds(ScrollableHeight / AnimationSpeed);
            var verticalAnimation = new DoubleAnimation
            {
                From = 0,
                To = ScrollableHeight,
                Duration = verticalDuration,
                BeginTime = delay,
            };

            var scrollDuration = TimeSpan.FromSeconds(2 * delay.TotalSeconds + Math.Max(horitzontalDuration.TotalSeconds, verticalDuration.TotalSeconds));

            TimeSpan opacityAnimationDuration = TimeSpan.FromSeconds(0.334);
            var opacityOutAnimation = new DoubleAnimation()
            {
                From = 1,
                To = 0,
                BeginTime = scrollDuration,
                Duration = opacityAnimationDuration,
            };

            var horizontalScrollResetAnimation = new DoubleAnimationUsingKeyFrames()
            {
                BeginTime = scrollDuration,
            };

            horizontalScrollResetAnimation.KeyFrames.Add(
                new DiscreteDoubleKeyFrame(0, opacityAnimationDuration)
            );

            var verticalScrollResetAnimation = new DoubleAnimationUsingKeyFrames()
            {
                BeginTime = scrollDuration,
            };

            verticalScrollResetAnimation.KeyFrames.Add(
                new DiscreteDoubleKeyFrame(0, opacityAnimationDuration)
            );

            storyboard = new Storyboard();

            storyboard.Children.Add(opacityOutAnimation);
            storyboard.Children.Add(horizontalScrollResetAnimation);
            storyboard.Children.Add(verticalScrollResetAnimation);

            if (ScrollableWidth > 0)
            {
                storyboard.Children.Add(horizontalAnimation);
                Storyboard.SetTarget(horizontalAnimation, this);
                Storyboard.SetTargetProperty(horizontalAnimation, new PropertyPath(HorizontalOffsetProperty));
            }

            if (ScrollableHeight > 0)
            {
                storyboard.Children.Add(verticalAnimation);
                Storyboard.SetTarget(verticalAnimation, this);
                Storyboard.SetTargetProperty(verticalAnimation, new PropertyPath(VerticalOffsetProperty));
            }

            Storyboard.SetTarget(opacityOutAnimation, this);
            Storyboard.SetTargetProperty(opacityOutAnimation, new PropertyPath(OpacityProperty));

            Storyboard.SetTarget(horizontalScrollResetAnimation, this);
            Storyboard.SetTargetProperty(horizontalScrollResetAnimation, new PropertyPath(HorizontalOffsetProperty));

            Storyboard.SetTarget(verticalScrollResetAnimation, this);
            Storyboard.SetTargetProperty(verticalScrollResetAnimation, new PropertyPath(VerticalOffsetProperty));

            storyboard.RepeatBehavior = RepeatBehavior.Forever;

            storyboard.Begin(this, true);
        }

        public static DependencyProperty IsAnimatingProperty =
            DependencyProperty.Register(nameof(IsAnimating), typeof(bool), typeof(AutoScrollViewer), 
                new PropertyMetadata(new PropertyChangedCallback(OnIsAnimatingChanged)));

        public AutoScrollViewer() : base()
        {

        }
    }
}
