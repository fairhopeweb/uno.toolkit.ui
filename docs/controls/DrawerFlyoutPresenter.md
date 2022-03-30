# DrawerFlyoutPresenter
## Summary
`DrawerFlyoutPresenter` is a special `ContentPresenter` to be used in the template of a `FlyoutPresenter` to enable gesture support.

## Properties
### Remarks
All of the properties below can be used both as a dependency property or as an attached property, much like the `ScrollView` properties:
```xml
<Style x:Key="CustomDrawerFlyoutPresenterStyle"
       BasedOn="{StaticResource DrawerFlyoutPresenterStyle}"
       TargetType="FlyoutPresenter"
       xmlns:utu="using:Uno.Toolkit.UI.Controls">
    <Setter Property="utu:DrawerFlyoutPresenter.OpenDirection" Value="Top" />
    <Setter Property="utu:DrawerFlyoutPresenter.LightDismissOverlayBackground" Value="#80808080" />
    <Setter Property="utu:DrawerFlyoutPresenter.IsGestureEnabled" Value="True" />
</Style>
<!-- ... -->
<utu:DrawerFlyoutPresenter xmlns:utu="using:Uno.Toolkit.UI.Controls"
                           OpenDirection="Top"
                           LightDismissOverlayBackground="#80808080"
                           IsGestureEnabled="True" />
```

### `OpenDirection`

Gets or sets a value that indicates the open direction of the drawer flyout. The default value is `Up`. Possible values are: `DrawerOpenDirection.Right`, `Left`, `Down`, `Up`.

### `LightDismissOverlayBackground`

Gets or sets a brush that describes the background color of the light-dismiss overlay. The default value is `#80808080` (from the default style).

### `IsGestureEnabled`

Gets or sets a value that indicates whether the flyout will respond to gesture (manipulation-related events). The default value is `true`.

## Usage

To use this, simply use a `Flyout` with `Placement="Full"` and one of the followings as the `FlyoutPresenterStyle`:
- `LeftDrawerFlyoutPresenterStyle`
- `TopDrawerFlyoutPresenterStyle`
- `RightDrawerFlyoutPresenterStyle`
- `BottomDrawerFlyoutPresenterStyle`

Example:
```xml
<Button Content="Bottom Drawer"
        xmlns:toolkit="using:Uno.UI.Toolkit">
    <Button.Flyout>
        <Flyout Placement="Full" FlyoutPresenterStyle="{StaticResource BottomDrawerFlyoutPresenterStyle}">
            <StackPanel toolkit:VisibleBoundsPadding.PaddingMask="All"
                        Background="SkyBlue"
                        MinHeight="200">
                <TextBlock Text="text" />
                <Button Content="button" />
                <Button Content="button" />
            </StackPanel>
        </Flyout>
    </Button.Flyout>
</Button>
```
> note: Here `VisibleBoundsPadding.PaddingMask` is used to prevent the content from being placed outside of the user-interactable area on mobile devices.
