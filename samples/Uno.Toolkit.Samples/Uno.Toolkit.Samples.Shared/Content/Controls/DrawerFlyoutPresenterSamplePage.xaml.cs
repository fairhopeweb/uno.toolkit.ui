using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Uno.Toolkit.Samples.Entities;
using Uno.Toolkit.UI.Controls;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace Uno.Toolkit.Samples.Content.Controls
{
	[SamplePage(SampleCategory.Controls, nameof(DrawerFlyoutPresenter))]
	public sealed partial class DrawerFlyoutPresenterSamplePage : Page
	{
		public DrawerFlyoutPresenterSamplePage()
		{
			this.InitializeComponent();
		}
	}
}
