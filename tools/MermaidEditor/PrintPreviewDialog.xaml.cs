using System.IO;
using System.Printing;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using WpfImage = System.Windows.Controls.Image;
using WpfRectangle = System.Windows.Shapes.Rectangle;
using WpfBrushes = System.Windows.Media.Brushes;

namespace MermaidEditor;

public partial class PrintPreviewDialog : Window
{
    // P/Invoke for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;
    private readonly BitmapSource _diagramImage;
    private readonly string _documentTitle;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private double _pageWidth;
    private double _pageHeight;
    private double _marginSize = 96; // 1 inch in pixels (96 DPI)
    private double _scale = 1.0;
    private List<BitmapSource> _pageImages = new();
    private PrintQueue? _selectedPrinter;
    private double _printerMinMargin = 0; // Minimum margin supported by printer

    public PrintPreviewDialog(BitmapSource diagramImage, string documentTitle)
    {
        InitializeComponent();
        _diagramImage = diagramImage;
        _documentTitle = documentTitle;
        
        // Set default page size (Letter)
        _pageWidth = 8.5 * 96; // 8.5 inches at 96 DPI
        _pageHeight = 11 * 96; // 11 inches at 96 DPI
        
        SourceInitialized += PrintPreviewDialog_SourceInitialized;
        Loaded += PrintPreviewDialog_Loaded;
        
        // Populate printer list
        PopulatePrinterList();
    }

    private void PopulatePrinterList()
    {
        try
        {
            var printServer = new LocalPrintServer();
            var printQueues = printServer.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
            
            foreach (var queue in printQueues)
            {
                PrinterCombo.Items.Add(new ComboBoxItem { Content = queue.Name, Tag = queue });
            }
            
            // Select default printer
            var defaultPrinter = printServer.DefaultPrintQueue;
            if (defaultPrinter != null)
            {
                for (int i = 0; i < PrinterCombo.Items.Count; i++)
                {
                    if (PrinterCombo.Items[i] is ComboBoxItem item && item.Tag is PrintQueue pq && pq.Name == defaultPrinter.Name)
                    {
                        PrinterCombo.SelectedIndex = i;
                        _selectedPrinter = pq;
                        UpdatePrinterMinMargins();
                        break;
                    }
                }
            }
            else if (PrinterCombo.Items.Count > 0)
            {
                PrinterCombo.SelectedIndex = 0;
            }
        }
        catch
        {
            // If we can't enumerate printers, add a placeholder
            PrinterCombo.Items.Add(new ComboBoxItem { Content = "(Select printer when printing)", IsEnabled = false });
            PrinterCombo.SelectedIndex = 0;
        }
    }

    private void PrinterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PrinterCombo.SelectedItem is ComboBoxItem item && item.Tag is PrintQueue queue)
        {
            _selectedPrinter = queue;
            UpdatePrinterMinMargins();
            ValidateMargins();
            UpdatePreview();
        }
    }

    private void UpdatePrinterMinMargins()
    {
        if (_selectedPrinter == null) return;
        
        try
        {
            var capabilities = _selectedPrinter.GetPrintCapabilities();
            if (capabilities.PageImageableArea != null)
            {
                // Get the unprintable margins (origin is the top-left of printable area)
                double leftMargin = capabilities.PageImageableArea.OriginWidth;
                double topMargin = capabilities.PageImageableArea.OriginHeight;
                
                // Calculate right and bottom margins
                double pageWidth = capabilities.OrientedPageMediaWidth ?? _pageWidth;
                double pageHeight = capabilities.OrientedPageMediaHeight ?? _pageHeight;
                double printableWidth = capabilities.PageImageableArea.ExtentWidth;
                double printableHeight = capabilities.PageImageableArea.ExtentHeight;
                
                double rightMargin = pageWidth - leftMargin - printableWidth;
                double bottomMargin = pageHeight - topMargin - printableHeight;
                
                // Use the largest minimum margin
                _printerMinMargin = Math.Max(Math.Max(leftMargin, topMargin), Math.Max(rightMargin, bottomMargin));
            }
        }
        catch
        {
            _printerMinMargin = 0;
        }
    }

    private void ValidateMargins()
    {
        if (_printerMinMargin > 0 && _marginSize < _printerMinMargin)
        {
            double minMarginInches = _printerMinMargin / 96.0;
            MarginWarningText.Text = $"Warning: Selected printer requires minimum {minMarginInches:F2}\" margins. Content may be clipped.";
            MarginWarningText.Visibility = Visibility.Visible;
        }
        else
        {
            MarginWarningText.Visibility = Visibility.Collapsed;
        }
    }

    private void PrintPreviewDialog_SourceInitialized(object? sender, EventArgs e)
    {
        UpdateTitleBarTheme();
    }

    private void UpdateTitleBarTheme()
    {
        try
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            if (hwnd != IntPtr.Zero)
            {
                var isDark = ThemeManager.IsDarkTheme;
                int darkModeValue = isDark ? 1 : 0;
                DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkModeValue, sizeof(int));
                
                // Set caption color based on theme
                var colors = ThemeManager.GetThemeColors(ThemeManager.CurrentTheme);
                int captionColor = ColorToInt(colors.Background);
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));
            }
        }
        catch
        {
            // Silently fail if DWM API is not available
        }
    }

    private static int ColorToInt(System.Windows.Media.Color color)
    {
        // Color format is 0x00BBGGRR (BGR, not RGB)
        return (color.B << 16) | (color.G << 8) | color.R;
    }

    private void PrintPreviewDialog_Loaded(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void PageSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PageSizeCombo.SelectedItem is ComboBoxItem item)
        {
            var content = item.Content.ToString();
            switch (content)
            {
                case "Letter (8.5 x 11 in)":
                    _pageWidth = 8.5 * 96;
                    _pageHeight = 11 * 96;
                    break;
                case "Legal (8.5 x 14 in)":
                    _pageWidth = 8.5 * 96;
                    _pageHeight = 14 * 96;
                    break;
                case "A4 (210 x 297 mm)":
                    _pageWidth = 210 / 25.4 * 96;
                    _pageHeight = 297 / 25.4 * 96;
                    break;
                case "A3 (297 x 420 mm)":
                    _pageWidth = 297 / 25.4 * 96;
                    _pageHeight = 420 / 25.4 * 96;
                    break;
                case "Tabloid (11 x 17 in)":
                    _pageWidth = 11 * 96;
                    _pageHeight = 17 * 96;
                    break;
            }
            UpdatePreview();
        }
    }

    private void Orientation_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void Scaling_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void MarginsCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MarginsCombo.SelectedItem is ComboBoxItem item)
        {
            var content = item.Content.ToString();
            switch (content)
            {
                case "Normal (1 inch)":
                    _marginSize = 96;
                    break;
                case "Narrow (0.5 inch)":
                    _marginSize = 48;
                    break;
                case "Wide (1.5 inch)":
                    _marginSize = 144;
                    break;
                case "None":
                    _marginSize = 0;
                    break;
            }
            ValidateMargins();
            UpdatePreview();
        }
    }

    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        UpdatePreview();
    }

    private void UpdatePreview()
    {
        if (_diagramImage == null || PreviewPagesPanel == null)
            return;

        PreviewPagesPanel.Children.Clear();
        _pageImages.Clear();

        // Get effective page dimensions based on orientation
        double effectivePageWidth = LandscapeRadio?.IsChecked == true ? _pageHeight : _pageWidth;
        double effectivePageHeight = LandscapeRadio?.IsChecked == true ? _pageWidth : _pageHeight;

        // Calculate printable area
        double printableWidth = effectivePageWidth - (2 * _marginSize);
        double printableHeight = effectivePageHeight - (2 * _marginSize);

        // Calculate scale based on scaling option
        // Note: CapturePreviewAsync captures at screen DPI, so we need to account for that
        double imageWidth = _diagramImage.PixelWidth;
        double imageHeight = _diagramImage.PixelHeight;
        
        // Get the actual DPI of the image (WebView2 captures at screen DPI, typically 96 or higher)
        double imageDpiX = _diagramImage.DpiX > 0 ? _diagramImage.DpiX : 96;
        double imageDpiY = _diagramImage.DpiY > 0 ? _diagramImage.DpiY : 96;
        
        // Convert image dimensions to 96 DPI equivalent for consistent calculations
        double normalizedImageWidth = imageWidth * 96 / imageDpiX;
        double normalizedImageHeight = imageHeight * 96 / imageDpiY;

        if (FitToPageRadio?.IsChecked == true)
        {
            // Scale to fit entire image on one page (never exceed 100%)
            double scaleX = printableWidth / normalizedImageWidth;
            double scaleY = printableHeight / normalizedImageHeight;
            _scale = Math.Min(Math.Min(scaleX, scaleY), 1.0);
            _totalPages = 1;
        }
        else if (FitToWidthRadio?.IsChecked == true)
        {
            // Scale to fit width, may span multiple pages vertically (never exceed 100%)
            _scale = Math.Min(printableWidth / normalizedImageWidth, 1.0);
            double scaledHeight = normalizedImageHeight * _scale;
            _totalPages = (int)Math.Ceiling(scaledHeight / printableHeight);
        }
        else // Actual size
        {
            _scale = 1.0;
            int pagesWide = (int)Math.Ceiling(normalizedImageWidth / printableWidth);
            int pagesHigh = (int)Math.Ceiling(normalizedImageHeight / printableHeight);
            _totalPages = pagesWide * pagesHigh;
        }

        // Update info text
        if (PageCountText != null)
            PageCountText.Text = $"Pages: {_totalPages}";
        if (ScaleText != null)
            ScaleText.Text = $"Scale: {_scale * 100:F0}%";

        // Generate preview pages
        GeneratePreviewPages(effectivePageWidth, effectivePageHeight, printableWidth, printableHeight);

        // Update navigation
        _currentPage = 1;
        UpdatePageNavigation();
    }

    private void GeneratePreviewPages(double pageWidth, double pageHeight, double printableWidth, double printableHeight)
    {
        // Use normalized dimensions for consistent scaling
        double imageDpiX = _diagramImage.DpiX > 0 ? _diagramImage.DpiX : 96;
        double imageDpiY = _diagramImage.DpiY > 0 ? _diagramImage.DpiY : 96;
        double normalizedImageWidth = _diagramImage.PixelWidth * 96 / imageDpiX;
        double normalizedImageHeight = _diagramImage.PixelHeight * 96 / imageDpiY;
        
        double scaledImageWidth = normalizedImageWidth * _scale;
        double scaledImageHeight = normalizedImageHeight * _scale;

        // Preview scale factor (to fit in the preview area)
        double previewScale = Math.Min(400 / pageWidth, 500 / pageHeight);

        if (FitToPageRadio?.IsChecked == true || (FitToWidthRadio?.IsChecked == true && _totalPages == 1))
        {
            // Single page
            var pagePreview = CreatePagePreview(pageWidth, pageHeight, previewScale, 0, 0, scaledImageWidth, scaledImageHeight, printableWidth, printableHeight);
            PreviewPagesPanel.Children.Add(pagePreview);
        }
        else if (FitToWidthRadio?.IsChecked == true)
        {
            // Multiple pages vertically
            for (int page = 0; page < _totalPages; page++)
            {
                double yOffset = page * printableHeight;
                var pagePreview = CreatePagePreview(pageWidth, pageHeight, previewScale, 0, yOffset, scaledImageWidth, scaledImageHeight, printableWidth, printableHeight);
                PreviewPagesPanel.Children.Add(pagePreview);
            }
        }
        else
        {
            // Actual size - may span multiple pages in both directions
            int pagesWide = (int)Math.Ceiling(scaledImageWidth / printableWidth);
            int pagesHigh = (int)Math.Ceiling(scaledImageHeight / printableHeight);

            for (int row = 0; row < pagesHigh; row++)
            {
                for (int col = 0; col < pagesWide; col++)
                {
                    double xOffset = col * printableWidth;
                    double yOffset = row * printableHeight;
                    var pagePreview = CreatePagePreview(pageWidth, pageHeight, previewScale, xOffset, yOffset, scaledImageWidth, scaledImageHeight, printableWidth, printableHeight);
                    PreviewPagesPanel.Children.Add(pagePreview);
                }
            }
        }
    }

    private Border CreatePagePreview(double pageWidth, double pageHeight, double previewScale, 
        double xOffset, double yOffset, double scaledImageWidth, double scaledImageHeight,
        double printableWidth, double printableHeight)
    {
        var pageBorder = new Border
        {
            Width = pageWidth * previewScale,
            Height = pageHeight * previewScale,
            Background = WpfBrushes.White,
            BorderBrush = WpfBrushes.Gray,
            BorderThickness = new Thickness(1),
            Margin = new Thickness(8),
            ClipToBounds = true
        };

        var canvas = new Canvas
        {
            Width = pageWidth * previewScale,
            Height = pageHeight * previewScale,
            ClipToBounds = true
        };

        // Create image for this page section
        var image = new WpfImage
        {
            Source = _diagramImage,
            Width = scaledImageWidth * previewScale,
            Height = scaledImageHeight * previewScale,
            Stretch = Stretch.Fill
        };

        // Position the image
        double imageX = (_marginSize - xOffset) * previewScale;
        double imageY = (_marginSize - yOffset) * previewScale;

        // Center on page if option is checked and image fits on page
        if (CenterOnPageCheck?.IsChecked == true && FitToPageRadio?.IsChecked == true)
        {
            double centeredX = (_marginSize + (printableWidth - scaledImageWidth) / 2) * previewScale;
            double centeredY = (_marginSize + (printableHeight - scaledImageHeight) / 2) * previewScale;
            imageX = centeredX;
            imageY = centeredY;
        }
        else if (CenterOnPageCheck?.IsChecked == true && FitToWidthRadio?.IsChecked == true)
        {
            // Center horizontally only for fit-to-width
            double centeredX = (_marginSize + (printableWidth - scaledImageWidth) / 2) * previewScale;
            imageX = centeredX;
            imageY = (_marginSize - yOffset) * previewScale;
        }

        Canvas.SetLeft(image, imageX);
        Canvas.SetTop(image, imageY);
        canvas.Children.Add(image);

        // Draw margin guides (light gray dashed lines)
        var marginRect = new WpfRectangle
        {
            Width = printableWidth * previewScale,
            Height = printableHeight * previewScale,
            Stroke = WpfBrushes.LightGray,
            StrokeDashArray = new DoubleCollection { 4, 2 },
            StrokeThickness = 0.5
        };
        Canvas.SetLeft(marginRect, _marginSize * previewScale);
        Canvas.SetTop(marginRect, _marginSize * previewScale);
        canvas.Children.Add(marginRect);

        pageBorder.Child = canvas;
        return pageBorder;
    }

    private void UpdatePageNavigation()
    {
        if (CurrentPageText != null)
            CurrentPageText.Text = $"Page {_currentPage} of {_totalPages}";
        if (PrevPageButton != null)
            PrevPageButton.IsEnabled = _currentPage > 1;
        if (NextPageButton != null)
            NextPageButton.IsEnabled = _currentPage < _totalPages;
    }

    private void PrevPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            UpdatePageNavigation();
            ScrollToPage(_currentPage);
        }
    }

    private void NextPage_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage < _totalPages)
        {
            _currentPage++;
            UpdatePageNavigation();
            ScrollToPage(_currentPage);
        }
    }

    private void ScrollToPage(int pageNumber)
    {
        if (PreviewPagesPanel.Children.Count >= pageNumber)
        {
            var page = PreviewPagesPanel.Children[pageNumber - 1] as FrameworkElement;
            page?.BringIntoView();
        }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        var printDialog = new System.Windows.Controls.PrintDialog();
        
        // Set selected printer if available
        if (_selectedPrinter != null)
        {
            printDialog.PrintQueue = _selectedPrinter;
        }
        
        // Set default page settings
        if (LandscapeRadio?.IsChecked == true)
        {
            printDialog.PrintTicket.PageOrientation = PageOrientation.Landscape;
        }
        else
        {
            printDialog.PrintTicket.PageOrientation = PageOrientation.Portrait;
        }

        if (printDialog.ShowDialog() == true)
        {
            PrintDocument(printDialog);
            DialogResult = true;
            Close();
        }
    }

    private void PrintDocument(System.Windows.Controls.PrintDialog printDialog)
    {
        // Get printer page dimensions
        var pageSize = new System.Windows.Size(printDialog.PrintableAreaWidth, printDialog.PrintableAreaHeight);
        
        // Calculate printable area
        double printableWidth = pageSize.Width - (2 * _marginSize);
        double printableHeight = pageSize.Height - (2 * _marginSize);

        // Use normalized dimensions for consistent scaling
        double imageDpiX = _diagramImage.DpiX > 0 ? _diagramImage.DpiX : 96;
        double imageDpiY = _diagramImage.DpiY > 0 ? _diagramImage.DpiY : 96;
        double normalizedImageWidth = _diagramImage.PixelWidth * 96 / imageDpiX;
        double normalizedImageHeight = _diagramImage.PixelHeight * 96 / imageDpiY;
        double scale;

        if (FitToPageRadio?.IsChecked == true)
        {
            double scaleX = printableWidth / normalizedImageWidth;
            double scaleY = printableHeight / normalizedImageHeight;
            scale = Math.Min(Math.Min(scaleX, scaleY), 1.0);
        }
        else if (FitToWidthRadio?.IsChecked == true)
        {
            scale = Math.Min(printableWidth / normalizedImageWidth, 1.0);
        }
        else
        {
            scale = 1.0;
        }

        double scaledWidth = normalizedImageWidth * scale;
        double scaledHeight = normalizedImageHeight * scale;

        // Create visual for printing
        if (FitToPageRadio?.IsChecked == true || (FitToWidthRadio?.IsChecked == true && scaledHeight <= printableHeight))
        {
            // Single page print
            var visual = CreatePrintVisual(pageSize, scaledWidth, scaledHeight, 0, printableWidth, printableHeight);
            printDialog.PrintVisual(visual, _documentTitle);
        }
        else
        {
            // Multi-page print using FixedDocument
            var document = new FixedDocument();
            document.DocumentPaginator.PageSize = pageSize;

            int totalPages = (int)Math.Ceiling(scaledHeight / printableHeight);

            for (int page = 0; page < totalPages; page++)
            {
                double yOffset = page * printableHeight;
                var visual = CreatePrintVisual(pageSize, scaledWidth, scaledHeight, yOffset, printableWidth, printableHeight);

                var pageContent = new PageContent();
                var fixedPage = new FixedPage
                {
                    Width = pageSize.Width,
                    Height = pageSize.Height
                };

                // Convert visual to UIElement
                var container = new Border { Child = visual };
                fixedPage.Children.Add(container);

                ((IAddChild)pageContent).AddChild(fixedPage);
                document.Pages.Add(pageContent);
            }

            printDialog.PrintDocument(document.DocumentPaginator, _documentTitle);
        }
    }

    private Canvas CreatePrintVisual(System.Windows.Size pageSize, double scaledWidth, double scaledHeight, 
        double yOffset, double printableWidth, double printableHeight)
    {
        var canvas = new Canvas
        {
            Width = pageSize.Width,
            Height = pageSize.Height,
            Background = PrintBackgroundCheck?.IsChecked == true ? WpfBrushes.White : WpfBrushes.Transparent
        };

        var image = new WpfImage
        {
            Source = _diagramImage,
            Width = scaledWidth,
            Height = scaledHeight,
            Stretch = Stretch.Fill
        };

        // Calculate position
        double marginInPrinterUnits = _marginSize;
        double x = marginInPrinterUnits;
        double y = marginInPrinterUnits - yOffset;

        // Center if option is checked
        if (CenterOnPageCheck?.IsChecked == true)
        {
            if (FitToPageRadio?.IsChecked == true)
            {
                x = marginInPrinterUnits + (printableWidth - scaledWidth) / 2;
                y = marginInPrinterUnits + (printableHeight - scaledHeight) / 2;
            }
            else if (FitToWidthRadio?.IsChecked == true)
            {
                x = marginInPrinterUnits + (printableWidth - scaledWidth) / 2;
            }
        }

        Canvas.SetLeft(image, x);
        Canvas.SetTop(image, y);

        // Clip to printable area
        canvas.Clip = new RectangleGeometry(new Rect(marginInPrinterUnits, marginInPrinterUnits, printableWidth, printableHeight));
        canvas.Children.Add(image);

        return canvas;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
