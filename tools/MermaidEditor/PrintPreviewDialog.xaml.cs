using System.Globalization;
using System.IO;
using System.Printing;
using System.Runtime.InteropServices;
using System.Text;
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
    private readonly bool _isMarkdown;
    private int _currentPage = 1;
    private int _totalPages = 1;
    private double _pageWidth;
    private double _pageHeight;
    private double _marginSize = 96; // 1 inch in pixels (96 DPI)
    private double _scale = 1.0;
    private List<BitmapSource> _pageImages = new();
    private PrintQueue? _selectedPrinter;
    private double _printerMinMargin = 0; // Minimum margin supported by printer

    public PrintPreviewDialog(BitmapSource diagramImage, string documentTitle, bool isMarkdown = false)
    {
        InitializeComponent();
        _diagramImage = diagramImage;
        _documentTitle = documentTitle;
        _isMarkdown = isMarkdown;
        
        // Set default page size (Letter)
        _pageWidth = 8.5 * 96; // 8.5 inches at 96 DPI
        _pageHeight = 11 * 96; // 11 inches at 96 DPI
        
        SourceInitialized += PrintPreviewDialog_SourceInitialized;
        Loaded += PrintPreviewDialog_Loaded;
        
        // Populate printer list
        PopulatePrinterList();
        
        // For markdown, default to "Fit to page width" since content typically spans multiple pages
        if (_isMarkdown)
        {
            FitToWidthRadio.IsChecked = true;
            FitToPageRadio.IsChecked = false;
        }
    }

    private const string SaveToPdfPrinterName = "Save to PDF";

    private bool IsSaveToPdfSelected => PrinterCombo.SelectedItem is ComboBoxItem item && item.Content?.ToString() == SaveToPdfPrinterName;

    private void PopulatePrinterList()
    {
        // Add virtual "Save to PDF" printer as first item
        PrinterCombo.Items.Add(new ComboBoxItem { Content = SaveToPdfPrinterName, Tag = null });

        try
        {
            var printServer = new LocalPrintServer();
            var printQueues = printServer.GetPrintQueues(new[] { EnumeratedPrintQueueTypes.Local, EnumeratedPrintQueueTypes.Connections });
            
            foreach (var queue in printQueues)
            {
                PrinterCombo.Items.Add(new ComboBoxItem { Content = queue.Name, Tag = queue });
            }
            
            // Select default printer (prefer real printer over Save to PDF)
            var defaultPrinter = printServer.DefaultPrintQueue;
            bool foundDefault = false;
            if (defaultPrinter != null)
            {
                for (int i = 0; i < PrinterCombo.Items.Count; i++)
                {
                    if (PrinterCombo.Items[i] is ComboBoxItem item && item.Tag is PrintQueue pq && pq.Name == defaultPrinter.Name)
                    {
                        PrinterCombo.SelectedIndex = i;
                        _selectedPrinter = pq;
                        UpdatePrinterMinMargins();
                        foundDefault = true;
                        break;
                    }
                }
            }
            
            // Fall back to "Save to PDF" if no default printer found
            if (!foundDefault)
            {
                PrinterCombo.SelectedIndex = 0;
            }
        }
        catch
        {
            // If we can't enumerate printers, "Save to PDF" is already there
            PrinterCombo.SelectedIndex = 0;
        }
    }

    private void PrinterCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PrinterCombo.SelectedItem is ComboBoxItem item)
        {
            if (item.Tag is PrintQueue queue)
            {
                _selectedPrinter = queue;
                UpdatePrinterMinMargins();
            }
            else
            {
                // Virtual printer (Save to PDF) - no hardware constraints
                _selectedPrinter = null;
                _printerMinMargin = 0;
            }
            
            // Update button text based on selection
            if (PrintButton != null)
            {
                PrintButton.Content = IsSaveToPdfSelected ? "Save as PDF" : "Print";
            }
            
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
        if (MarginWarningText == null) return;
        
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

        // Update page range defaults
        if (ToPageTextBox != null && AllPagesRadio?.IsChecked == true)
            ToPageTextBox.Text = _totalPages.ToString();

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

        // Clip to printable area (margins) so content doesn't extend beyond
        canvas.Clip = new RectangleGeometry(new Rect(
            _marginSize * previewScale, 
            _marginSize * previewScale, 
            printableWidth * previewScale, 
            printableHeight * previewScale));

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

        // Draw margin guides (more visible dashed lines)
        var marginRect = new WpfRectangle
        {
            Width = printableWidth * previewScale,
            Height = printableHeight * previewScale,
            Stroke = new SolidColorBrush(System.Windows.Media.Color.FromRgb(180, 180, 180)),
            StrokeDashArray = new DoubleCollection { 6, 3 },
            StrokeThickness = 1.0
        };
        Canvas.SetLeft(marginRect, _marginSize * previewScale);
        Canvas.SetTop(marginRect, _marginSize * previewScale);
        canvas.Children.Add(marginRect);

        pageBorder.Child = canvas;
        return pageBorder;
    }

    private void PageRange_Changed(object sender, RoutedEventArgs e)
    {
        if (FromPageTextBox == null || ToPageTextBox == null) return;
        bool isRange = PageRangeRadio?.IsChecked == true;
        FromPageTextBox.IsEnabled = isRange;
        ToPageTextBox.IsEnabled = isRange;
    }

    private void PageRangeValue_Changed(object sender, TextChangedEventArgs e)
    {
        // Validation happens at print/save time
    }

    /// <summary>
    /// Returns the 1-based (fromPage, toPage) range clamped to [1, _totalPages].
    /// </summary>
    private (int from, int to) GetPageRange()
    {
        if (AllPagesRadio?.IsChecked == true)
            return (1, _totalPages);

        int from = 1, to = _totalPages;
        if (int.TryParse(FromPageTextBox?.Text, out int f))
            from = Math.Max(1, Math.Min(f, _totalPages));
        if (int.TryParse(ToPageTextBox?.Text, out int t))
            to = Math.Max(from, Math.Min(t, _totalPages));
        return (from, to);
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
        // Route to PDF save if "Save to PDF" virtual printer is selected
        if (IsSaveToPdfSelected)
        {
            SaveAsPdf_Click(sender, e);
            return;
        }

        var printDialog = new System.Windows.Controls.PrintDialog();
        
        // Set selected printer - use selected printer or default
        if (_selectedPrinter != null)
        {
            printDialog.PrintQueue = _selectedPrinter;
        }
        
        // Apply orientation from our preview settings
        try
        {
            if (LandscapeRadio?.IsChecked == true)
            {
                printDialog.PrintTicket.PageOrientation = PageOrientation.Landscape;
            }
            else
            {
                printDialog.PrintTicket.PageOrientation = PageOrientation.Portrait;
            }
        }
        catch (System.Printing.PrintQueueException)
        {
            // PrintTicket provider failed to bind — fall back to system print dialog
            // so the user can select a working printer
            if (printDialog.ShowDialog() != true) return;
        }

        // Print directly without showing system dialog (we already have our own preview)
        try
        {
            PrintDocument(printDialog);
        }
        catch (System.Printing.PrintQueueException ex)
        {
            System.Windows.MessageBox.Show(
                $"Unable to print: the selected printer could not be initialized.\n\n{ex.Message}\n\nPlease try selecting a different printer.",
                "Print Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            return;
        }
        DialogResult = true;
        Close();
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

        // Apply page range
        var (fromPage, toPage) = GetPageRange();

        // Create visual for printing
        if (FitToPageRadio?.IsChecked == true || (FitToWidthRadio?.IsChecked == true && scaledHeight <= printableHeight))
        {
            // Single page print (only print if page 1 is in range)
            if (fromPage <= 1 && toPage >= 1)
            {
                var visual = CreatePrintVisual(pageSize, scaledWidth, scaledHeight, 0, printableWidth, printableHeight);
                printDialog.PrintVisual(visual, _documentTitle);
            }
        }
        else
        {
            // Multi-page print using FixedDocument
            var document = new FixedDocument();
            document.DocumentPaginator.PageSize = pageSize;

            int totalPages = (int)Math.Ceiling(scaledHeight / printableHeight);

            for (int page = 0; page < totalPages; page++)
            {
                int pageNum = page + 1; // 1-based
                if (pageNum < fromPage || pageNum > toPage)
                    continue;

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

    private void SaveAsPdf_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "PDF Document (*.pdf)|*.pdf",
            Title = "Save as PDF",
            DefaultExt = ".pdf",
            FileName = Path.GetFileNameWithoutExtension(_documentTitle) + ".pdf"
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            // Use the same page layout logic as printing
            double effectivePageWidth = LandscapeRadio?.IsChecked == true ? _pageHeight : _pageWidth;
            double effectivePageHeight = LandscapeRadio?.IsChecked == true ? _pageWidth : _pageHeight;
            var pageSize = new System.Windows.Size(effectivePageWidth, effectivePageHeight);

            double printableWidth = pageSize.Width - (2 * _marginSize);
            double printableHeight = pageSize.Height - (2 * _marginSize);

            // Calculate scale (same logic as PrintDocument)
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

            // Render each page using DrawingVisual (renders synchronously, no visual tree needed)
            const double renderDpi = 300;
            double dpiScale = renderDpi / 96.0;

            // Page size in PDF points (1 point = 1/72 inch)
            double pdfPageWidthPt = effectivePageWidth / 96.0 * 72.0;
            double pdfPageHeightPt = effectivePageHeight / 96.0 * 72.0;

            var pageJpegData = new List<byte[]>();
            var pagePixelWidths = new List<int>();
            var pagePixelHeights = new List<int>();

            // Apply page range
            var (fromPage, toPage) = GetPageRange();

            // Determine page offsets
            var allPageOffsets = new List<double>();
            if (FitToPageRadio?.IsChecked == true || (FitToWidthRadio?.IsChecked == true && scaledHeight <= printableHeight))
            {
                allPageOffsets.Add(0);
            }
            else
            {
                int totalPages = (int)Math.Ceiling(scaledHeight / printableHeight);
                for (int page = 0; page < totalPages; page++)
                    allPageOffsets.Add(page * printableHeight);
            }

            // Filter to selected page range
            var pageOffsets = new List<double>();
            for (int i = 0; i < allPageOffsets.Count; i++)
            {
                int pageNum = i + 1; // 1-based
                if (pageNum >= fromPage && pageNum <= toPage)
                    pageOffsets.Add(allPageOffsets[i]);
            }

            foreach (double yOffset in pageOffsets)
            {
                // Use DrawingVisual with DrawingContext.DrawImage for reliable off-screen rendering
                var drawingVisual = new DrawingVisual();
                using (var dc = drawingVisual.RenderOpen())
                {
                    // Draw white background
                    if (PrintBackgroundCheck?.IsChecked == true)
                    {
                        dc.DrawRectangle(WpfBrushes.White, null, new Rect(0, 0, pageSize.Width, pageSize.Height));
                    }

                    // Calculate image position
                    double x = _marginSize;
                    double y = _marginSize - yOffset;

                    if (CenterOnPageCheck?.IsChecked == true)
                    {
                        if (FitToPageRadio?.IsChecked == true)
                        {
                            x = _marginSize + (printableWidth - scaledWidth) / 2;
                            y = _marginSize + (printableHeight - scaledHeight) / 2;
                        }
                        else if (FitToWidthRadio?.IsChecked == true)
                        {
                            x = _marginSize + (printableWidth - scaledWidth) / 2;
                        }
                    }

                    // Clip to printable area
                    dc.PushClip(new RectangleGeometry(new Rect(_marginSize, _marginSize, printableWidth, printableHeight)));

                    // Draw the image directly (synchronous, no visual tree dependency)
                    dc.DrawImage(_diagramImage, new Rect(x, y, scaledWidth, scaledHeight));

                    dc.Pop(); // pop clip
                }

                int pixelWidth = (int)(pageSize.Width * dpiScale);
                int pixelHeight = (int)(pageSize.Height * dpiScale);

                var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, renderDpi, renderDpi, PixelFormats.Pbgra32);
                rtb.Render(drawingVisual);

                // Encode as JPEG for smaller file size
                var encoder = new JpegBitmapEncoder { QualityLevel = 95 };
                encoder.Frames.Add(BitmapFrame.Create(rtb));

                using var ms = new MemoryStream();
                encoder.Save(ms);
                pageJpegData.Add(ms.ToArray());
                pagePixelWidths.Add(pixelWidth);
                pagePixelHeights.Add(pixelHeight);
            }

            // Write PDF file
            WritePdf(dialog.FileName, pageJpegData, pagePixelWidths, pagePixelHeights, pdfPageWidthPt, pdfPageHeightPt);

            System.Windows.MessageBox.Show("PDF saved successfully!", "Save Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show($"Failed to save PDF: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>
    /// Writes a valid PDF file with JPEG images as pages. No external dependencies required.
    /// </summary>
    private static void WritePdf(string filePath, List<byte[]> pageJpegData, List<int> pixelWidths, 
        List<int> pixelHeights, double pageWidthPt, double pageHeightPt)
    {
        using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write);
        // Map object number → byte offset for correct xref table
        var objectOffsets = new Dictionary<int, long>();
        int objNum = 1;

        // Helper to write a string
        void Write(string s)
        {
            var bytes = Encoding.ASCII.GetBytes(s);
            fs.Write(bytes, 0, bytes.Length);
        }

        void WriteBytes(byte[] data)
        {
            fs.Write(data, 0, data.Length);
        }

        string F(double v) => v.ToString("F2", CultureInfo.InvariantCulture);

        // PDF Header
        Write("%PDF-1.4\n");
        // Binary comment to indicate binary content
        Write("%\xe2\xe3\xcf\xd3\n");

        int pageCount = pageJpegData.Count;

        // Pre-allocate ALL object numbers upfront:
        // 1: Catalog
        // 2: Pages
        // Per page: page obj, content stream obj, image XObject
        int catalogObj = objNum++;
        int pagesObj = objNum++;
        int[] pageObjs = new int[pageCount];
        int[] contentObjs = new int[pageCount];
        int[] imageObjs = new int[pageCount];
        for (int i = 0; i < pageCount; i++)
        {
            pageObjs[i] = objNum++;
            contentObjs[i] = objNum++;
            imageObjs[i] = objNum++;
        }

        // Object 1: Catalog
        objectOffsets[catalogObj] = fs.Position;
        Write($"{catalogObj} 0 obj\n<< /Type /Catalog /Pages {pagesObj} 0 R >>\nendobj\n");

        // Object 2: Pages
        objectOffsets[pagesObj] = fs.Position;
        var kids = string.Join(" ", pageObjs.Select(p => $"{p} 0 R"));
        Write($"{pagesObj} 0 obj\n<< /Type /Pages /Kids [ {kids} ] /Count {pageCount} >>\nendobj\n");

        // Write page objects, content streams, and image XObjects for each page
        for (int i = 0; i < pageCount; i++)
        {
            string imgName = $"Img{i}";
            // Page content: draw image scaled to full page
            string contentStream = $"q {F(pageWidthPt)} 0 0 {F(pageHeightPt)} 0 0 cm /{imgName} Do Q\n";
            byte[] contentBytes = Encoding.ASCII.GetBytes(contentStream);

            // Page object
            objectOffsets[pageObjs[i]] = fs.Position;
            Write($"{pageObjs[i]} 0 obj\n");
            Write("<< /Type /Page ");
            Write($"/Parent {pagesObj} 0 R ");
            Write($"/MediaBox [ 0 0 {F(pageWidthPt)} {F(pageHeightPt)} ] ");
            Write($"/Contents {contentObjs[i]} 0 R ");
            Write($"/Resources << /XObject << /{imgName} {imageObjs[i]} 0 R >> >> ");
            Write(">>\nendobj\n");

            // Content stream object
            objectOffsets[contentObjs[i]] = fs.Position;
            Write($"{contentObjs[i]} 0 obj\n");
            Write($"<< /Length {contentBytes.Length} >>\n");
            Write("stream\n");
            WriteBytes(contentBytes);
            Write("endstream\nendobj\n");

            // Image XObject
            byte[] jpeg = pageJpegData[i];
            objectOffsets[imageObjs[i]] = fs.Position;
            Write($"{imageObjs[i]} 0 obj\n");
            Write("<< /Type /XObject /Subtype /Image ");
            Write($"/Width {pixelWidths[i]} /Height {pixelHeights[i]} ");
            Write("/ColorSpace /DeviceRGB /BitsPerComponent 8 ");
            Write("/Filter /DCTDecode ");
            Write($"/Length {jpeg.Length + 1} ");
            Write(">>\n");
            Write("stream\n");
            WriteBytes(jpeg);
            Write("\nendstream\nendobj\n");
        }

        // Cross-reference table
        long xrefStart = fs.Position;
        int totalObjs = objNum; // objNum is one past the last object
        Write("xref\n");
        Write($"0 {totalObjs}\n");
        Write("0000000000 65535 f \n"); // free object entry (obj 0)
        for (int i = 1; i < totalObjs; i++)
        {
            Write($"{objectOffsets[i]:D10} 00000 n \n");
        }

        // Trailer
        Write("trailer\n");
        Write($"<< /Size {totalObjs} /Root {catalogObj} 0 R >>\n");
        Write("startxref\n");
        Write($"{xrefStart}\n");
        Write("%%EOF\n");
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void PreviewScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (PreviewPagesPanel.Children.Count == 0 || _totalPages <= 1)
            return;

        // Calculate which page is most visible based on scroll position
        double scrollOffset = e.VerticalOffset;
        double viewportHeight = e.ViewportHeight;
        double viewportCenter = scrollOffset + (viewportHeight / 2);

        // Find which page contains the center of the viewport
        double cumulativeHeight = 0;
        int visiblePage = 1;

        for (int i = 0; i < PreviewPagesPanel.Children.Count; i++)
        {
            if (PreviewPagesPanel.Children[i] is FrameworkElement page)
            {
                double pageHeight = page.ActualHeight + page.Margin.Top + page.Margin.Bottom;
                cumulativeHeight += pageHeight;

                if (cumulativeHeight >= viewportCenter)
                {
                    visiblePage = i + 1;
                    break;
                }
            }
        }

        // Update current page if changed
        if (visiblePage != _currentPage)
        {
            _currentPage = visiblePage;
            UpdatePageNavigation();
        }
    }
}
