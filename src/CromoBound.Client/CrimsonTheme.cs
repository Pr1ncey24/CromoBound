using MudBlazor;

namespace CromoBound.Client;

/// <summary>The approved look for MudBlazor's own components: the Crimson night palette, Fredoka titles and Nunito text, rounded
/// shapes. The app's layout and tiles take the same values from <c>wwwroot/css/app.css</c>.</summary>
public static class CrimsonTheme
{
    private static readonly string[] Body = ["Nunito", "system-ui", "sans-serif"];
    private static readonly string[] Display = ["Fredoka", "Nunito", "sans-serif"];

    public static MudTheme Theme { get; } = new()
    {
        PaletteDark = new PaletteDark
        {
            Primary = "#d8392a",
            PrimaryContrastText = "#ffffff",
            Secondary = "#ff8a7a",
            SecondaryContrastText = "#0b0909",
            Error = "#b8284f",
            ErrorContrastText = "#ffffff",
            Info = "#bcd6f5",
            Success = "#4fc3a1",
            Warning = "#e3913f",
            Background = "#0b0909",
            Surface = "#161112",
            AppbarBackground = "#1a1314",
            DrawerBackground = "#161112",
            TextPrimary = "#f2e9e6",
            TextSecondary = "#b39d99",
            TextDisabled = "#7a6764",
            ActionDefault = "#d7c7c3",
            ActionDisabled = "#7a6764",
            ActionDisabledBackground = "#211819",
            LinesDefault = "#3a2a2b",
            LinesInputs = "#3a2a2b",
            Divider = "#2a1f20",
            OverlayDark = "rgba(8,4,4,0.82)",
        },
        Typography = new Typography
        {
            Default = new DefaultTypography { FontFamily = Body },
            H1 = new H1Typography { FontFamily = Display },
            H2 = new H2Typography { FontFamily = Display },
            H3 = new H3Typography { FontFamily = Display },
            H4 = new H4Typography { FontFamily = Display },
            H5 = new H5Typography { FontFamily = Display },
            H6 = new H6Typography { FontFamily = Display },
            Button = new ButtonTypography { FontFamily = Body, TextTransform = "none", FontWeight = "600" },
        },
        LayoutProperties = new LayoutProperties { DefaultBorderRadius = "14px" },
    };
}
