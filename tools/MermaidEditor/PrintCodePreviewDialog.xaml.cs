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
using WpfBrushes = System.Windows.Media.Brushes;

namespace MermaidEditor;

public partial class PrintCodePreviewDialog : Window
{
    // P/Invoke for dark title bar
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);
    
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    private const int DWMWA_CAPTION_COLOR = 35;
    private const string SaveToPdfPrinterName = "Save to PDF";

    private readonly string _code;
    private readonly string _documentTitle;
    private double _fontSize = 10;
    private bool _showLineNumbers = false;
    private bool _wordWrap = true;
    private FlowDocument? _flowDocument;
    private PrintQueue? _selectedPrinter;
    private double _printerMinMargin = 0;
    private double _pageWidth;
    private double _pageHeight;
    private double _marginSize = 96; // 1 inch in pixels (96 DPI)
    private int _currentPage = 1;
    private int _totalPages = 1;

    private bool IsSaveToPdfSelected => PrinterCombo.SelectedItem is ComboBoxItem item && item.Content?.ToString() == SaveToPdfPrinterName;

    public PrintCodePreviewDialog(string code, string documentTitle)
    {
        InitializeComponent();
        _code = code;
        _documentTitle = documentTitle;
        
        // Set default page size (Letter)
        _pageWidth = 8.5 * 96; // 8.5 inches at 96 DPI
        _pageHeight = 11 * 96; // 11 inches at 96 DPI
        
        SourceInitialized += PrintCodePreviewDialog_SourceInitialized;
        Loaded += PrintCodePreviewDialog_Loaded;
        
        // Populate printer list
        PopulatePrinterList();
    }

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
                double leftMargin = capabilities.PageImageableArea.OriginWidth;
                double topMargin = capabilities.PageImageableArea.OriginHeight;
                
                double pageWidth = capabilities.OrientedPageMediaWidth ?? _pageWidth;
                double pageHeight = capabilities.OrientedPageMediaHeight ?? _pageHeight;
                double printableWidth = capabilities.PageImageableArea.ExtentWidth;
                double printableHeight = capabilities.PageImageableArea.ExtentHeight;
                
                double rightMargin = pageWidth - leftMargin - printableWidth;
                double bottomMargin = pageHeight - topMargin - printableHeight;
                
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

    private void PrintCodePreviewDialog_SourceInitialized(object? sender, EventArgs e)
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
        return (color.B << 16) | (color.G << 8) | color.R;
    }

    private void PrintCodePreviewDialog_Loaded(object sender, RoutedEventArgs e)
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

    private void FontSizeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (FontSizeCombo.SelectedItem is ComboBoxItem item)
        {
            var content = item.Content.ToString();
            _fontSize = content switch
            {
                "8 pt" => 8,
                "9 pt" => 9,
                "10 pt" => 10,
                "11 pt" => 11,
                "12 pt" => 12,
                "14 pt" => 14,
                _ => 10
            };
            UpdatePreview();
        }
    }

    private void Options_Changed(object sender, RoutedEventArgs e)
    {
        _showLineNumbers = ShowLineNumbersCheckBox?.IsChecked == true;
        _wordWrap = WordWrapCheckBox?.IsChecked == true;
        UpdatePreview();
    }

    private FlowDocument BuildFlowDocument(double pageWidth, double pageHeight)
    {
        // Content area width = page width minus left and right margins
        double contentWidth = pageWidth - (_marginSize * 2);
        
        var doc = new FlowDocument
        {
            PagePadding = new Thickness(_marginSize),
            PageWidth = pageWidth,
            PageHeight = pageHeight,
            FontFamily = new System.Windows.Media.FontFamily("Consolas, Courier New, monospace"),
            FontSize = _fontSize,
            // When word wrap is on, set column width to content area so text wraps within margins
            // When off, set to MaxValue so lines extend without wrapping
            ColumnWidth = _wordWrap ? Math.Max(contentWidth, 100) : double.MaxValue
        };

        // Add title
        var titleParagraph = new Paragraph(new Run(_documentTitle))
        {
            FontSize = _fontSize + 4,
            FontWeight = FontWeights.Bold,
            Margin = new Thickness(0, 0, 0, 20),
            Foreground = WpfBrushes.Black
        };
        doc.Blocks.Add(titleParagraph);

        // Add code lines
        var lines = _code.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        int lineNumberWidth = lines.Length.ToString().Length;
        
        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var paragraph = new Paragraph
            {
                FontSize = _fontSize,
                Margin = new Thickness(0, 0, 0, 2),
                TextAlignment = System.Windows.TextAlignment.Left,
                Foreground = WpfBrushes.Black
            };
            
            if (_showLineNumbers)
            {
                var lineNumber = (i + 1).ToString().PadLeft(lineNumberWidth) + "  ";
                var lineNumberRun = new Run(lineNumber)
                {
                    Foreground = WpfBrushes.Gray
                };
                paragraph.Inlines.Add(lineNumberRun);
            }
            
            paragraph.Inlines.Add(new Run(string.IsNullOrEmpty(line) ? " " : line));
            doc.Blocks.Add(paragraph);
        }

        // Update line count
        if (LineCountText != null)
            LineCountText.Text = $"Lines: {lines.Length}";

        return doc;
    }

    private void UpdatePreview()
    {
        if (PreviewPagesPanel == null) return;

        PreviewPagesPanel.Children.Clear();

        // Get effective page dimensions based on orientation
        double effectivePageWidth = LandscapeRadio?.IsChecked == true ? _pageHeight : _pageWidth;
        double effectivePageHeight = LandscapeRadio?.IsChecked == true ? _pageWidth : _pageHeight;

        // Build the FlowDocument with actual page dimensions
        _flowDocument = BuildFlowDocument(effectivePageWidth, effectivePageHeight);

        // Force pagination by getting the document paginator
        var paginator = ((IDocumentPaginatorSource)_flowDocument).DocumentPaginator;
        paginator.PageSize = new System.Windows.Size(effectivePageWidth, effectivePageHeight);

        // Force layout computation
        paginator.ComputePageCount();
        _totalPages = paginator.PageCount;

        // Preview scale factor
        double previewScale = Math.Min(400 / effectivePageWidth, 500 / effectivePageHeight);

        // Render each page as a visual for the preview
        for (int i = 0; i < _totalPages; i++)
        {
            var page = paginator.GetPage(i);
            
            // Render page to bitmap
            int pixelWidth = (int)(effectivePageWidth * 1.5); // 144 DPI for preview quality
            int pixelHeight = (int)(effectivePageHeight * 1.5);
            var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, 144, 144, PixelFormats.Pbgra32);
            rtb.Render(page.Visual);

            // Create preview image
            var image = new System.Windows.Controls.Image
            {
                Source = rtb,
                Width = effectivePageWidth * previewScale,
                Height = effectivePageHeight * previewScale,
                Stretch = Stretch.Uniform
            };

            var pageBorder = new Border
            {
                Width = effectivePageWidth * previewScale,
                Height = effectivePageHeight * previewScale,
                Background = WpfBrushes.White,
                BorderBrush = WpfBrushes.Gray,
                BorderThickness = new Thickness(1),
                Margin = new Thickness(8),
                ClipToBounds = true,
                Child = image
            };

            PreviewPagesPanel.Children.Add(pageBorder);
        }

        // Update info
        if (PageCountText != null)
            PageCountText.Text = $"Pages: {_totalPages}";

        _currentPage = 1;
        UpdatePageNavigation();
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

    private void PreviewScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (PreviewPagesPanel.Children.Count == 0 || _totalPages <= 1)
            return;

        double scrollOffset = e.VerticalOffset;
        double viewportHeight = e.ViewportHeight;
        double viewportCenter = scrollOffset + (viewportHeight / 2);

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

        if (visiblePage != _currentPage)
        {
            _currentPage = visiblePage;
            UpdatePageNavigation();
        }
    }

    private void Print_Click(object sender, RoutedEventArgs e)
    {
        // Route to PDF save if "Save to PDF" virtual printer is selected
        if (IsSaveToPdfSelected)
        {
            SaveAsPdf();
            return;
        }

        if (_flowDocument == null) return;

        var printDialog = new System.Windows.Controls.PrintDialog();
        
        // Set selected printer
        if (_selectedPrinter != null)
        {
            printDialog.PrintQueue = _selectedPrinter;
        }
        
        // Apply orientation
        if (LandscapeRadio?.IsChecked == true)
        {
            printDialog.PrintTicket.PageOrientation = PageOrientation.Landscape;
        }
        else
        {
            printDialog.PrintTicket.PageOrientation = PageOrientation.Portrait;
        }

        // Get effective page dimensions
        double effectivePageWidth = LandscapeRadio?.IsChecked == true ? _pageHeight : _pageWidth;
        double effectivePageHeight = LandscapeRadio?.IsChecked == true ? _pageWidth : _pageHeight;

        // Rebuild document with correct page dimensions for printing
        var printDoc = BuildFlowDocument(effectivePageWidth, effectivePageHeight);
        var paginator = ((IDocumentPaginatorSource)printDoc).DocumentPaginator;
        paginator.PageSize = new System.Windows.Size(effectivePageWidth, effectivePageHeight);

        // Print directly without showing system dialog
        printDialog.PrintDocument(paginator, $"Code: {_documentTitle}");
        DialogResult = true;
        Close();
    }

    private void SaveAsPdf()
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
            double effectivePageWidth = LandscapeRadio?.IsChecked == true ? _pageHeight : _pageWidth;
            double effectivePageHeight = LandscapeRadio?.IsChecked == true ? _pageWidth : _pageHeight;

            // Build document for PDF rendering
            var pdfDoc = BuildFlowDocument(effectivePageWidth, effectivePageHeight);
            var paginator = ((IDocumentPaginatorSource)pdfDoc).DocumentPaginator;
            paginator.PageSize = new System.Windows.Size(effectivePageWidth, effectivePageHeight);
            paginator.ComputePageCount();

            // PDF page size in points (1 point = 1/72 inch)
            double pdfPageWidthPt = effectivePageWidth / 96.0 * 72.0;
            double pdfPageHeightPt = effectivePageHeight / 96.0 * 72.0;

            const double renderDpi = 300;
            double dpiScale = renderDpi / 96.0;

            var pageJpegData = new List<byte[]>();
            var pagePixelWidths = new List<int>();
            var pagePixelHeights = new List<int>();

            for (int i = 0; i < paginator.PageCount; i++)
            {
                var page = paginator.GetPage(i);

                int pixelWidth = (int)(effectivePageWidth * dpiScale);
                int pixelHeight = (int)(effectivePageHeight * dpiScale);

                var rtb = new RenderTargetBitmap(pixelWidth, pixelHeight, renderDpi, renderDpi, PixelFormats.Pbgra32);
                rtb.Render(page.Visual);

                // Encode as JPEG
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
        var objectOffsets = new Dictionary<int, long>();
        int objNum = 1;

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
        Write("%\xe2\xe3\xcf\xd3\n");

        int pageCount = pageJpegData.Count;

        // Pre-allocate ALL object numbers upfront
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

        // Write page objects, content streams, and image XObjects
        for (int i = 0; i < pageCount; i++)
        {
            string imgName = $"Img{i}";
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
            Write($"/Length {jpeg.Length} ");
            Write(">>\n");
            Write("stream\n");
            WriteBytes(jpeg);
            Write("\nendstream\nendobj\n");
        }

        // Cross-reference table
        long xrefStart = fs.Position;
        int totalObjs = objNum;
        Write("xref\n");
        Write($"0 {totalObjs}\n");
        Write("0000000000 65535 f \n");
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
}
