#if __IOS__ || __ANDROID__
#define HAS_NATIVE_NAVBAR
#endif
#if __IOS__
using UIKit;
#endif
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Uno.Disposables;
using Windows.Foundation;
using Windows.UI.Core;
using Uno.Toolkit.UI.Extensions;
#if HAS_UNO
using Uno.UI.Helpers;
#endif
#if IS_WINUI
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Navigation;
#else
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;
#endif

namespace Uno.Toolkit.UI.Controls
{
	/// <summary>
	/// Represents a specialized app bar that provides layout for AppBarButton and navigation logic.
	/// </summary>
	[ContentProperty(Name = nameof(PrimaryCommands))]
	[TemplatePart(Name = NavigationBarPresenter, Type = typeof(FrameworkElement))]
	public partial class NavigationBar : ContentControl
	{
		public event EventHandler<object>? Closed;
		public event EventHandler<object>? Opened;
		public event EventHandler<object>? Closing;
		public event EventHandler<object>? Opening;
		public event TypedEventHandler<NavigationBar, DynamicOverflowItemsChangingEventArgs?>? DynamicOverflowItemsChanging;

		private const string MoreButton = "MoreButton";
		private const string PrimaryItemsControl = "PrimaryItemsControl";
		private const string SecondaryItemsControl = "SecondaryItemsControl";
		private const string NavigationBarPresenter = "NavigationBarPresenter";


#if HAS_NATIVE_NAVBAR
		private bool _isNativeTemplate;
#endif

		private INavigationBarPresenter? _presenter;
		private SerialDisposable _backRequestedHandler = new SerialDisposable();
		private SerialDisposable _frameBackStackChangedHandler = new SerialDisposable();
		private WeakReference<Page?>? _pageRef;
		private bool _isIconSourceSetByNavBar = false;
		private bool _isMainCommandStyleSetByNavBar = false;

		public NavigationBar()
		{
			MainCommand ??= new AppBarButton();
			PrimaryCommands ??= new NavigationBarElementCollection();
			SecondaryCommands ??= new NavigationBarElementCollection();

			Loaded += OnLoaded;
			Unloaded += OnUnloaded;
			DefaultStyleKey = typeof(NavigationBar);
		}

		protected override void OnApplyTemplate()
		{
			base.OnApplyTemplate();

			GetTemplatePart(NavigationBarPresenter, out _presenter);

			_presenter?.SetOwner(this);

#if HAS_NATIVE_NAVBAR
			_isNativeTemplate = _presenter is NativeNavigationBarPresenter;
#endif
		}

#if !HAS_NATIVE_NAVBAR
		private void UpdateMainCommandIconSource()
		{
			var iconSource = MainCommandIconSource;
			if (iconSource is { })
			{
				var mainCommand = MainCommand;
				var localValue = this.ReadLocalValue(NavigationBar.MainCommandIconSourceProperty);

				if (localValue == DependencyProperty.UnsetValue || _isIconSourceSetByNavBar)
				{
					IconElement? icon = null;
					if (iconSource is FontIconSource fis)
					{
						var fontIcon = new FontIcon();
						fontIcon.Glyph = fis.Glyph;
						fontIcon.FontSize = fis.FontSize;

						if (fis.Foreground is { })
						{
							fontIcon.Foreground = fis.Foreground;
						}

						if (fis.FontFamily is { })
						{
							fontIcon.FontFamily = fis.FontFamily;
						}

						fontIcon.FontWeight = fis.FontWeight;
						fontIcon.FontStyle = fis.FontStyle;
						fontIcon.IsTextScaleFactorEnabled = fis.IsTextScaleFactorEnabled;
						fontIcon.MirroredWhenRightToLeft = fis.MirroredWhenRightToLeft;

						icon = fontIcon;
					}
					else if (iconSource is SymbolIconSource sis)
					{
						var symbolIcon = new SymbolIcon();

						symbolIcon.Symbol = sis.Symbol;

						if (sis.Foreground is { })
						{
							symbolIcon.Foreground = sis.Foreground;
						}

						icon = symbolIcon;
					}
					else if (iconSource is BitmapIconSource bis)
					{
						BitmapIcon bitmapIcon = new BitmapIcon();

						if (bis.UriSource is { })
						{
							bitmapIcon.UriSource = bis.UriSource;
						}

						bitmapIcon.ShowAsMonochrome = bis.ShowAsMonochrome;

						if (bis.Foreground is { } foreground)
        {
							bitmapIcon.Foreground = foreground;
						}

						icon = bitmapIcon;
					}
					else if (iconSource is PathIconSource pis)
					{
						PathIcon pathIcon = new PathIcon();

						if (pis.Data is PathGeometry pathData)
						{
							pathIcon.Data = pis.Data;
						}
						if (pis.Foreground is { } newForeground)
						{
							pathIcon.Foreground = newForeground;
						}

						icon = pathIcon;
					}

					if (icon != null)
					{
						mainCommand.Icon = icon;
						_isIconSourceSetByNavBar = true;
					}
					else
					{
						mainCommand.ClearValue(AppBarButton.IconProperty);
						_isIconSourceSetByNavBar = false;
					}
				}
			}
		}
#endif

		internal bool TryPerformBack()
		{
			if (MainCommandMode != MainCommandMode.Back)
			{
				return false;
			}

			Page? page = null;
			if (_pageRef?.TryGetTarget(out page) ?? false)
			{
				if (page?.Frame is { Visibility: Visibility.Visible } frame
					&& frame.CurrentSourcePageType == page.GetType()
					&& frame.CanGoBack)
				{
					frame.GoBack();
					return true;
				}
			}

			return false;
		}

		#region Event Raising
		internal void RaiseClosingEvent(object e) 
			=> Closing?.Invoke(this, e);

		internal void RaiseClosedEvent(object e) 
			=> Closed?.Invoke(this, e);
		
		internal void RaiseOpeningEvent(object e)
			=> Opening?.Invoke(this, e);
		
		internal void RaiseOpenedEvent(object e)
			=> Opened?.Invoke(this, e);

		internal void RaiseDynamicOverflowItemsChanging(DynamicOverflowItemsChangingEventArgs args)
			=> DynamicOverflowItemsChanging?.Invoke(this, args);
		#endregion

		private void OnUnloaded(object sender, RoutedEventArgs e)
		{
			_backRequestedHandler.Disposable = null;
			_frameBackStackChangedHandler.Disposable = null;
		}

		private void OnLoaded(object sender, RoutedEventArgs e)
		{
			_pageRef = new WeakReference<Page?>(this.GetFirstParent<Page>());

			SystemNavigationManager.GetForCurrentView().BackRequested += OnBackRequested;
			_backRequestedHandler.Disposable = Disposable.Create(() => SystemNavigationManager.GetForCurrentView().BackRequested -= OnBackRequested);

#if !HAS_NATIVE_NAVBAR
			Page? page = null;
			if (_pageRef?.TryGetTarget(out page) ?? false)
			{
				var frame = page?.Frame;
				if (frame?.BackStack is ObservableCollection<PageStackEntry> backStack)
				{
					backStack.CollectionChanged += OnBackStackChanged;
					_frameBackStackChangedHandler.Disposable = Disposable.Create(() => backStack.CollectionChanged -= OnBackStackChanged);
				}
			}
#endif
			UpdateMainCommandVisibility();
		}


#if !HAS_NATIVE_NAVBAR
		private void OnBackStackChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			UpdateMainCommandVisibility();
		}
#endif

		internal void UpdateMainCommandVisibility()
		{
			if (MainCommandMode != MainCommandMode.Back)
			{
				return;
			}

			Page? page = null;
			if ((_pageRef?.TryGetTarget(out page) ?? false) && MainCommand is { })
			{
				var buttonVisibility = (page?.Frame?.CanGoBack ?? false)
					? Visibility.Visible
					: Visibility.Collapsed;

				MainCommand.Visibility = buttonVisibility;
			}
		}

		private void OnBackRequested(object? sender, BackRequestedEventArgs e)
		{
			if (!e.Handled && MainCommandMode == MainCommandMode.Back)
			{
				e.Handled = TryPerformBack();
			}
		}

		private void OnPropertyChanged(DependencyPropertyChangedEventArgs args)
		{
			if (args.Property == MainCommandProperty)
			{
				UpdateMainCommandStyle();
				UpdateMainCommandVisibility();
			}
			else if (args.Property == MainCommandModeProperty)
			{
				UpdateMainCommandVisibility();
			}
			else if (args.Property == MainCommandStyleProperty)
			{
				UpdateMainCommandStyle();
			}
#if !HAS_NATIVE_NAVBAR
			else if (args.Property == MainCommandIconSourceProperty)
			{
				UpdateMainCommandIconSource();
			}
#endif
		}

		private void UpdateMainCommandStyle()
		{
			var mainCommand = MainCommand;
			var localStyleValue = mainCommand.Style?.ReadLocalValue(StyleProperty);
			var mainCommandStyle = MainCommandStyle;

			if (localStyleValue == DependencyProperty.UnsetValue || _isMainCommandStyleSetByNavBar)
			{
				if (mainCommandStyle != null)
				{
					mainCommand.Style = mainCommandStyle;
					_isMainCommandStyleSetByNavBar = true;
				}
				else
				{
					mainCommand.ClearValue(StyleProperty);
					_isMainCommandStyleSetByNavBar = false;
				}
			}
		}

		private void GetTemplatePart<T>(string name, out T? element) where T : class
		{
			element = GetTemplateChild(name) as T;
		}
	}
}
