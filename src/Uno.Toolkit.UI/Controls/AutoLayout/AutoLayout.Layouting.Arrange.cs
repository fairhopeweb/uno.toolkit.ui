using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Windows.Foundation;

#if IS_WINUI
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
#else
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
#endif

namespace Uno.Toolkit.UI;

partial class AutoLayout
{
	protected override Size ArrangeOverride(Size finalSize)
	{
		// Local cache of DependencyProperties
		var children = Children;
		var orientation = Orientation;
		var justify = Justify;
		var primaryAxisAlignment = PrimaryAxisAlignment;
		var padding = Padding;
		var spacing = Spacing.FiniteOrDefault(0d);
		var isHorizontal = orientation == Orientation.Horizontal;
		var borderThickness = BorderBrush is null ? default : BorderThickness;

		var borderThicknessLength = borderThickness.GetLength(orientation);

		var totalNonFilledStackedSize = 0d;
		var totalOfFillMaxSize = 0d; 
		var numberOfFilledChildren = 0;
		var numberOfFilledChildrenWithMax = 0;
		var numberOfStackedChildren = 0;
		var haveStartPadding = false;
		var haveEndPadding = false;

		if (_calculatedChildren is null || children.Count != _calculatedChildren.Length)
		{
			// Children list changed, invalidate measure and wait for next pass
			InvalidateMeasure();
			return finalSize;
		}

		// 1. Calculate the total size of non-filled and filled children
		for (var i = 0; i < children.Count; i++)
		{
			var child = children[i];
			var calculatedChild = _calculatedChildren[i];

			if (child is FrameworkElement { Visibility: Visibility.Collapsed })
			{
				continue;
			}

			if (!ReferenceEquals(child, calculatedChild.Element))
			{
				// Invalid calculated child, invalidate measure and wait for next pass to fix this.
				InvalidateMeasure();
				return finalSize;
			}

			switch (calculatedChild.Role)
			{
				case AutoLayoutRole.Independent:
					continue; // Independent children are not stacked, skip it from the calculation
				case AutoLayoutRole.Filled:
					numberOfFilledChildren++;
					if (calculatedChild.Element as FrameworkElement is { } frameworkElement && double.IsInfinity(frameworkElement.GetMaxLength(orientation)) is false)
					{
						var maxLenght = frameworkElement.GetMaxLength(orientation);
						numberOfFilledChildrenWithMax++;
						totalOfFillMaxSize = totalOfFillMaxSize + maxLenght;
					}
					break;
				case AutoLayoutRole.Hug:
				case AutoLayoutRole.Fixed:
				default:
					totalNonFilledStackedSize += calculatedChild.MeasuredLength;
					break;
			}

			numberOfStackedChildren++;
		}

		var atLeastOneFilledChild = numberOfFilledChildren > 0;

		switch (primaryAxisAlignment)
		{
			case AutoLayoutAlignment.Start:
				haveStartPadding = true;
				haveEndPadding = atLeastOneFilledChild || justify == AutoLayoutJustify.SpaceBetween;
				break;
			case AutoLayoutAlignment.End:
				haveStartPadding = atLeastOneFilledChild || justify == AutoLayoutJustify.SpaceBetween;
				haveEndPadding = true;
				break;
			case AutoLayoutAlignment.Center:
				var havePadding = atLeastOneFilledChild || justify == AutoLayoutJustify.SpaceBetween;
				haveEndPadding = havePadding;
				haveStartPadding = havePadding;
				break;
		}

		var startPadding = haveStartPadding
			? isHorizontal ? padding.Left : padding.Top
			: 0d;
		var endPadding = haveEndPadding
			? isHorizontal ? padding.Right : padding.Bottom
			: 0d;

		var totalPaddingSize = startPadding + endPadding;

		// Available Size is the final size minus the border thickness and the padding
		var availableSizeForStackedChildren = finalSize.GetLength(orientation) - (borderThicknessLength + totalPaddingSize);
		EnsureZeroFloor(ref availableSizeForStackedChildren);

		// Start the offset at the border + start padding
		var currentOffset = borderThickness.GetStartLength(orientation) + startPadding;

		// Calculate the defined inter-element spacing size (if any, not taking care of SpaceBetween yet)
		var totalSpacingSize = spacing * (numberOfStackedChildren - 1);

		// Calculate the remaining size for each filled children (if any)
		var filledChildrenSize = atLeastOneFilledChild
			? (availableSizeForStackedChildren - (totalNonFilledStackedSize + totalSpacingSize)) / numberOfFilledChildren
			: 0;

		var filledMaxSurplus = (filledChildrenSize * numberOfFilledChildrenWithMax) - totalOfFillMaxSize;

		EnsureZeroFloor(ref filledMaxSurplus);

		var filledChildrenSizeAfterMaxSize = numberOfFilledChildren - numberOfFilledChildrenWithMax > 0 ?
			filledChildrenSize + (filledMaxSurplus / (numberOfFilledChildren - numberOfFilledChildrenWithMax)) :
		    filledChildrenSize + GetChildrenLowerThanAllocateSurplus(_calculatedChildren, filledChildrenSize);

		 EnsureZeroFloor(ref filledChildrenSizeAfterMaxSize);

		// Establish actual inter-element spacing.
		var spacingOffset = justify == AutoLayoutJustify.SpaceBetween && !atLeastOneFilledChild
			// When SpaceBetween is defined and there's no filled children
			? ComputeSpaceBetween(availableSizeForStackedChildren, totalNonFilledStackedSize, numberOfStackedChildren)
			// Fallback to the Spacing property
			: spacing;


		var haveMoreFilled = (numberOfFilledChildren - numberOfFilledChildrenWithMax) > 0;

		var primaryAxisAlignmentOffset = PrimaryAxisAlignmentOffsetSize(
			totalOfFillMaxSize,
			primaryAxisAlignment,
			availableSizeForStackedChildren,
			totalNonFilledStackedSize,
			haveMoreFilled,
			spacing,
			numberOfStackedChildren,
			filledChildrenSizeAfterMaxSize);

		currentOffset += primaryAxisAlignmentOffset;

		// 3. Arrange children, one by one (in reverse order if IsReverseZIndex is true)
		var isReverse = IsReverseZIndex;
		var start = isReverse ? children.Count - 1 : 0;
		var stop = isReverse ? -1 : children.Count;
		var increment = isReverse ? -1 : 1;
		for (var i = start; i != stop; i += increment)
		{
			var child = _calculatedChildren[i];

			if (child.Role == AutoLayoutRole.Independent)
			{
				// Independent is given all the available space,
				// because it's not stacking with others.
				// No padding is applied to it either.
				child.Element.Arrange(new Rect(default, finalSize));

				continue; // next child, current offset remains unchanged
			}

			if (children[i] is FrameworkElement { Visibility: Visibility.Collapsed })
			{
				continue;
			}

			// Calculate the length of the child (size in the panel orientation)

			// Length depends on the role of the child
			var childLength = child.Role == AutoLayoutRole.Filled
				? Math.Min(child.MeasuredLength, filledChildrenSizeAfterMaxSize)
				: child.MeasuredLength;

			var offsetRelativeToPadding = currentOffset - (startPadding + borderThicknessLength);

			if (childLength > availableSizeForStackedChildren - offsetRelativeToPadding)
			{
				// Child is too big, truncate it to the remaining space
				childLength = availableSizeForStackedChildren - offsetRelativeToPadding;
			}

			EnsureZeroFloor(ref childLength);

			// Calculate the position of the child by applying the alignment instructions
			var counterAlignment = GetCounterAlignment(child.Element);
			var isStretch = counterAlignment is AutoLayoutAlignment.Stretch || (child.Element is FrameworkElement fe && fe.GetCounterLength(orientation) is double.NaN);
			var haveCounterStartPadding = isStretch || counterAlignment is AutoLayoutAlignment.Start;
			var counterStartPadding = haveCounterStartPadding ? (isHorizontal ? padding.Top : padding.Left) : 0;

			var haveCounterEndPadding = counterAlignment is AutoLayoutAlignment.Stretch or AutoLayoutAlignment.End;
			var counterEndPadding = haveCounterEndPadding ? (isHorizontal ? padding.Bottom : padding.Right) : 0;

			var counterBorderLength = borderThickness.GetCounterLength(orientation);

			var availableCounterLength = finalSize.GetCounterLength(orientation) - (counterStartPadding + counterEndPadding + counterBorderLength);
			EnsureZeroFloor(ref availableCounterLength);

			var childSize = isHorizontal
				? new Size(childLength, availableCounterLength)
				: new Size(availableCounterLength, childLength);

			ApplyMinMaxValues(child.Element, orientation, ref childSize);

			var counterAlignmentOffset =
				ComputeCounterAlignmentOffset(counterAlignment, childSize.GetCounterLength(orientation), availableCounterLength, counterStartPadding, borderThickness.GetCounterStartLength(orientation));

			var childOffsetPosition = new Point(
				isHorizontal ? currentOffset : counterAlignmentOffset,
				isHorizontal ? counterAlignmentOffset : currentOffset);

			// Arrange the child to its final position, determined by the calculated offset and size
			child.Element.Arrange(new Rect(childOffsetPosition, childSize));

			// Increment the offset for the next child
			currentOffset += (childLength + spacingOffset);
		}

		return finalSize;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static void ApplyMinMaxValues(UIElement element, Orientation orientation, ref Size desiredSize)
	{
		if (element is not FrameworkElement frameworkElement)
		{
			return;
		}

		// Cache local values to avoid multiple calls to the DP system
		var width = frameworkElement.Width;
		var height = frameworkElement.Height;
		var maxWidth = frameworkElement.MaxWidth;
		var maxHeight = frameworkElement.MaxHeight;
		var minWidth = frameworkElement.MinWidth;
		var minHeight = frameworkElement.MinHeight;

		var primaryLength = GetPrimaryLength(element);
		var counterLength = GetCounterLength(element);

		// Apply primaryLength and counterLength constraints, if defined
		if (orientation is Orientation.Horizontal)
		{
			if (!double.IsNaN(primaryLength)) desiredSize.Width = primaryLength;
			if (!double.IsNaN(counterLength)) desiredSize.Height = counterLength;
		}
		else
		{
			if (!double.IsNaN(primaryLength)) desiredSize.Height = primaryLength;
			if (!double.IsNaN(counterLength)) desiredSize.Width = counterLength;
		}

		// Apply Width and Height constraints, if defined
		if (!double.IsNaN(width)) desiredSize.Width = width;
		if (!double.IsNaN(height)) desiredSize.Height = height;

		// Apply MaxWidth and MaxHeight constraints, if defined
		if (!double.IsNaN(maxWidth) && desiredSize.Width > maxWidth) desiredSize.Width = maxWidth;
		if (!double.IsNaN(maxHeight) && desiredSize.Height > maxHeight) desiredSize.Height = maxHeight;

		// Apply MinWidth and MinHeight constraints
		if (!double.IsNaN(minWidth) && desiredSize.Width < minWidth) desiredSize.Width = minWidth;
		if (!double.IsNaN(minHeight) && desiredSize.Height < minHeight) desiredSize.Height = minHeight;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double ComputeCounterAlignmentOffset(
		AutoLayoutAlignment autoLayoutAlignment,
		double childCounterLength,
		double availableCounterLength,
		double counterStartPadding,
		double counterBorderThickness)
	{
		var alignmentOffsetSize = availableCounterLength - childCounterLength;

		var calculatedOffset =  autoLayoutAlignment switch
		{
			AutoLayoutAlignment.End => alignmentOffsetSize,
			AutoLayoutAlignment.Center when alignmentOffsetSize > 0 => alignmentOffsetSize / 2,
			_ => 0
		};

		return calculatedOffset + counterStartPadding + counterBorderThickness;
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double ComputeSpaceBetween(
		double availableSizeForStackedChildren,
		double totalNonFilledStackedSize,
		int numberOfStackedChildren)
	{
		var availableSizeForStackedSpaceBetween = availableSizeForStackedChildren - totalNonFilledStackedSize;

		EnsureZeroFloor(ref availableSizeForStackedSpaceBetween);

		return availableSizeForStackedSpaceBetween / (numberOfStackedChildren - 1);
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double PrimaryAxisAlignmentOffsetSize(
		double totalOfFillMaxSize,
		AutoLayoutAlignment autoLayoutAlignment,
		double availableSizeForStackedChildren,
		double totalNonFilledStackedSize,
		bool haveMoreFilled,
		double spacing,
		int numberOfStackedChildren,
		double filledChildrenSizeAfterMaxSize)
	{
		if (haveMoreFilled || autoLayoutAlignment is AutoLayoutAlignment.Start)
		{
			return 0;
		}

		var totalSpacingSize = spacing * (numberOfStackedChildren - 1);

		var fillOverflow = filledChildrenSizeAfterMaxSize > 0 ? totalOfFillMaxSize : filledChildrenSizeAfterMaxSize;

		var alignmentOffsetSize = availableSizeForStackedChildren -
			(totalNonFilledStackedSize + totalSpacingSize + fillOverflow);

		 return autoLayoutAlignment switch
		{
			AutoLayoutAlignment.End when (filledChildrenSizeAfterMaxSize == 0 || filledChildrenSizeAfterMaxSize > 0 && alignmentOffsetSize > 0 ) => alignmentOffsetSize,
			AutoLayoutAlignment.Center when alignmentOffsetSize > 0 => alignmentOffsetSize / 2,
			_ => 0
		};
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	private static double GetChildrenLowerThanAllocateSurplus(CalculatedChildren[] calculatedChildren, double filledChildrenSizeBeforeMaxSize)
	{
		int countFilledChildrenGreaterThanAllocated = 0;
		double sumSurplusFromLower = 0d;

		for (int i = 0; i < calculatedChildren.Length; i++)
		{
			if (calculatedChildren[i].Role is AutoLayoutRole.Filled)
			{
				if (calculatedChildren[i].MeasuredLength > filledChildrenSizeBeforeMaxSize)
				{
					countFilledChildrenGreaterThanAllocated++;
				}
				else
				{
					sumSurplusFromLower += filledChildrenSizeBeforeMaxSize - calculatedChildren[i].MeasuredLength;
				}
			}
		}

		return countFilledChildrenGreaterThanAllocated > 0 ? sumSurplusFromLower / countFilledChildrenGreaterThanAllocated : 0;
	}
}
