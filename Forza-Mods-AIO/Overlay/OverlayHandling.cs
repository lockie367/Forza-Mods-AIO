﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forza_Mods_AIO.Resources;
using IniParser;
using static System.Convert;
using static System.Environment;
using static System.Windows.Visibility;
using static Forza_Mods_AIO.MainWindow;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;
using Timer = System.Windows.Forms.Timer;
using static Forza_Mods_AIO.Overlay.Overlay;
using static Forza_Mods_AIO.Overlay.Overlay.OptionType;
using static Forza_Mods_AIO.Resources.DllImports;
using TextAlignment = System.Windows.TextAlignment;

namespace Forza_Mods_AIO.Overlay;

public partial class OverlayHandling
{
    #region Blur DLLImports
    //Credits to Rafael Rivera for the blur https://github.com/riverar/sample-win32-acrylicblur
    internal enum AccentState
    {
        AccentEnableBlurBehind = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AccentPolicy
    {
        public AccentState AccentState;
        public uint AccentFlags;
        public uint GradientColor;
        public uint AnimationId;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WindowCompositionAttributeData
    {
        public WindowCompositionAttribute Attribute;
        public IntPtr Data;
        public int SizeOfData;
    }

    private enum WindowCompositionAttribute
    {
        WcaAccentPolicy = 19
    }

    [LibraryImport("user32.dll")]
    private static partial void SetWindowCompositionAttribute(IntPtr hwnd, ref WindowCompositionAttributeData data);
    #endregion
    #region Variables
    public static Keys Up = Keys.NumPad8;
    public static Keys Down = Keys.NumPad2;
    public static Keys Left = Keys.NumPad4;
    public static Keys Right = Keys.NumPad6;
    public static Keys Confirm = Keys.NumPad5;
    public static Keys Leave = Keys.NumPad0;
    public static Keys RapidAdjust = Keys.Alt;
    public static Keys OverlayVisibility = Keys.Subtract;
    // Menu operational vars
    private string[] _menuHeaders = null!;
    private int _selectedOptionIndex, _levelIndex;
    public string CurrentMenu = "MainOptions";
    private bool _hidden;
    private double _posTop, _posLeft;
    public float FontSize = 5;
    public string FontWeight = "Normal";
    public string FontStyle = "Normal";
    public int XOffset = 0, YOffset = 0; 

    private readonly string _settingsFilePath = GetFolderPath(SpecialFolder.MyDocuments) + @"\Forza Mods AIO\Overlay Settings.ini";
    
    private readonly Dictionary<int, string> _history = new()
    {
        {  0 ,"MainOptions" }
    };

    // Key vars
    private bool _upKeyDown, _downKeyDown, _leftKeyDown, _rightKeyDown, _rapidKeyDown;

    //Theme vars
    public Brush MainBackColour = Brushes.Transparent;
    public Brush DescriptionBackColour = Brushes.Transparent;
    public Brush MainBorderColour = Brushes.Black;
    public Brush DescriptionBorderColour = Brushes.Black;
    public int HeaderIndex = 0;
    public readonly List<object[]> Headers = new();
    private BitmapImage _headerImage = null!;
    #endregion
    // Caches all the headers
    public void CacheHeaders()
    {
        if (!Directory.Exists(CurrentDirectory + @"\Overlay\Headers"))
        {
            return;
        }

        _menuHeaders = Directory.GetFiles(CurrentDirectory + @"\Overlay\Headers");
        foreach (var header in _menuHeaders)
        {
            var inCachedBitmaps = Headers.Any(item => item[0].ToString().Contains(header.Split('\\').Last().Split('.').First()));
            if (inCachedBitmaps) continue;
            Headers.Add(new object[] { header.Split('\\').Last().Split('.').First(), new BitmapImage(new Uri(header)) });
        }
    }
        
    public void LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
        {
            return;
        }

        try
        {
            var parser = new FileIniDataParser();
            var iniData = parser.ReadFile(_settingsFilePath);

            foreach (var menuOption in typeof(SettingsMenu.SettingsMenu)
                         .GetFields(BindingFlags.Public | BindingFlags.Static)
                         .Where(f => f.FieldType == typeof(MenuOption)))
            {
                var mainQuery = menuOption.Name.Contains("Main") ? "Main" : "Description";

                if (menuOption.Name.Contains("Font"))
                {
                    mainQuery = "Font";
                }
                    
                var name = menuOption.Name;
                var value = iniData[mainQuery][name];

                var type = (OptionType)menuOption.FieldType.GetProperty("Type").GetValue(menuOption.GetValue(O.Sm));

                switch (type)
                {
                    case Float:
                    {
                        if (float.TryParse(value, out var result))
                        {
                            menuOption.FieldType.GetProperty("Value").SetValue(menuOption.GetValue(O.Sm), result);
                        }
                        break;
                    }
                    case Selection:
                    case Int:
                    {
                        if (int.TryParse(value, out var result))
                        {
                            menuOption.FieldType.GetProperty("Value").SetValue(menuOption.GetValue(O.Sm), result);
                        }
                        break;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            Debug.WriteLine(ex.StackTrace);
        }       
    }

    public void SaveSettings()
    {
        if (!File.Exists(_settingsFilePath))
        {
            using (File.Create(_settingsFilePath)) ;
        }

        try
        {
            var parser = new FileIniDataParser();
            var iniData = parser.ReadFile(_settingsFilePath);

            foreach (var menuOption in typeof(SettingsMenu.SettingsMenu)
                         .GetFields(BindingFlags.Public | BindingFlags.Static)
                         .Where(f => f.FieldType == typeof(MenuOption)))
            {
                var mainQuery = menuOption.Name.Contains("Main") ? "Main" : "Description";

                if (menuOption.Name.Contains("Font"))
                {
                    mainQuery = "Font";
                }
                    
                var name = menuOption.GetType().GetProperty("Name")?.GetValue(menuOption)?.ToString();
                var valueProperty = menuOption.FieldType.GetProperty("Value");
                if (valueProperty == null) continue;
                var value = valueProperty.GetValue(menuOption.GetValue(null));
                iniData[mainQuery][name] = value?.ToString();
            }
            
            parser.WriteFile(_settingsFilePath, iniData);
        }
        catch (Exception ex)
        {
            Debug.WriteLine(ex.Message);
            Debug.WriteLine(ex.StackTrace);
        }
    }
        
    // Handles the position of the overlay
    public void OverlayPosAndScale(CancellationToken ct)
    {
        CacheHeaders();
        LoadSettings();
        while (true)
        {
            Task.Delay(10, ct).Wait(ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            var forzaWindow = new DllImports.Rect();

            GetWindowRect(Mw.Gvp.Process.MainWindowHandle, ref forzaWindow);
            GetClientRect(Mw.Gvp.Process.MainWindowHandle, out var forzaClientWindow);

            var offset = forzaClientWindow.Bottom / 20d;
            _posLeft = forzaWindow.Left + (forzaWindow.Right - forzaWindow.Left - forzaClientWindow.Right) / 2d + offset;
            _posTop = forzaWindow.Top + (forzaWindow.Bottom - forzaWindow.Top - forzaClientWindow.Bottom) / 1.5d + offset;
            
            // top right
            // saving in case I need it
            //var xOffset = ForzaClientWindow.Bottom / 2.5d;
            //var PosLeft = ForzaWindow.Right - (ForzaWindow.Right - ForzaWindow.Left - ForzaClientWindow.Right) - xOffset;
            
            var YRes = forzaClientWindow.Bottom - (forzaWindow.Bottom - forzaWindow.Top - forzaClientWindow.Bottom) / 1.3;

            // Calculate the right numbers for the menu to scale to resolution
            double HeaderY = YRes / 10.8d, HeaderX = HeaderY * 4;

            SelectHeader();

            if (Mw.Gvp.Process.MainWindowHandle != GetForegroundWindow())
            {
                O?.Dispatcher.Invoke(() => O.Hide());
                continue;
            }
            
            O?.Dispatcher.Invoke(() =>
            {
                // Set position
                O.Top = _posTop;
                O.Left = _posLeft;

                // Set font options
                HandleFontSettings();
                    
                // Set width of menu and set header size (scale with resolution)
                O.Width = HeaderX;
                O.TopSection.Height = new GridLength(HeaderY);

                O.Header.Width = O.Width;
                O.Header.Height = O.TopSection.ActualHeight;

                // Set height of menu depending on items present
                if (O.OptionsBlock.Inlines.Count == O.AllMenus[CurrentMenu].Count * 2 - 1)
                {
                    O.MainSection.Height = new GridLength(O.OptionsBlock.ActualHeight + 10);
                    O.DescriptionSection.Height = O.DescriptionBlock.Text != string.Empty 
                        ? new GridLength(O.DescriptionBlock.ActualHeight + 15)
                        : new GridLength(0);

                    O.Height = O.TopSection.ActualHeight + O.MainSection.ActualHeight + O.DescriptionSection.ActualHeight;
                }

                // Set menu header image
                O.Header.Source = _headerImage;
                    
                // Set colours of menu
                O.MainBorder.Background = MainBackColour;
                O.MainBorder.BorderBrush = MainBorderColour;

                O.DescriptionBorder.Background = DescriptionBackColour;
                O.DescriptionBorder.BorderBrush = DescriptionBorderColour;

                if (O.Visibility != Hidden || _hidden) return;
                
                O.Show();
            });
        }
    }

    private void SelectHeader()
    {
        if (!Directory.Exists(CurrentDirectory + @"\Overlay\Headers") || _menuHeaders.Length == 0)
        {
            if (_headerImage is { IsFrozen: true })
            {
                _headerImage = _headerImage.Clone();
            }
            _headerImage = new BitmapImage(new Uri("pack://application:,,,/Overlay/Headers/pog header.png", UriKind.RelativeOrAbsolute));
            _headerImage.Dispatcher.Invoke(() => _headerImage.Freeze());
        }
        else if (_headerImage.UriSource.LocalPath != _menuHeaders[HeaderIndex])
        {
            if (_headerImage is { IsFrozen: true })
            {
                _headerImage = _headerImage.Clone();
            }
            _headerImage = (BitmapImage)Headers.Find(x => x[0].ToString().Contains(_menuHeaders[HeaderIndex].Split('\\').Last().Split('.').First()))[1];
            _headerImage.Dispatcher.Invoke(() => _headerImage.Freeze());
        }
    }

    private void HandleFontSettings()
    {
        foreach (var field in typeof(FontWeights).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.PropertyType != typeof(FontWeight))
            {
                continue;
            }
    
            var fontWeight = (FontWeight)field.GetValue(null);
            var cleanFontWeight = FontWeight.Replace(" ", "");
    
            if (!string.Equals(fontWeight.ToString(), cleanFontWeight, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }
            O.FontWeight = fontWeight;
            break;
        }
        
        foreach (var field in typeof(FontStyles).GetProperties(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.PropertyType != typeof(FontStyle))
            {
                continue;
            }
            var fontStyle = (FontStyle)field.GetValue(null);
    
            if (!string.Equals(fontStyle.ToString(), FontStyle, StringComparison.CurrentCultureIgnoreCase))
            {
                continue;
            }
            O.FontStyle = fontStyle;
            break;
        }
    }
    
    // Updates the menu, eg selected option, values etc
    public void UpdateMenuOptions(CancellationToken ct)
    {
        while (true)
        {
            Task.Delay(10, ct).Wait(ct);
            if (ct.IsCancellationRequested)
                return;
            
            // Clears the menu
            O.Dispatcher.BeginInvoke((Action)delegate
            {
                O.OptionsBlock.Inlines.Clear(); 
                O.ValueBlock.Inlines.Clear();
            });
            int index = 0;

            // Gets y resolution of the forza client window
            DllImports.Rect forzaWindow = new DllImports.Rect();

            GetWindowRect(Mw.Gvp.Process.MainWindowHandle, ref forzaWindow);
            GetClientRect(Mw.Gvp.Process.MainWindowHandle, out var forzaClientWindow);

            var yRes = forzaClientWindow.Bottom - (forzaWindow.Bottom - forzaWindow.Top - forzaClientWindow.Bottom) / 1.3;

            // Selected option background
            O.Dispatcher.Invoke((Action)delegate ()
            {
                if (O.OptionsBlock.Inlines.Count <= 1) return;
                // Remove previous highlight box
                foreach (UIElement child in O.Layout.Children)
                {
                    if ((string)child.GetType().GetProperty("Name")?.GetValue(child)! != "Highlight")
                    {
                        continue;
                    }
                    O.Layout.Children.Remove(child);
                    break;
                }
                // Create new highlight box
                var height = (float)(O.OptionsBlock.ActualHeight / O.AllMenus[CurrentMenu].Count * _selectedOptionIndex + 5);
                var highlighted = new Border
                {
                    Name = "Highlight",
                    VerticalAlignment = VerticalAlignment.Top,
                    Background = Brushes.Black,
                    Width = O.Layout.ActualWidth,
                    Height = O.OptionsBlock.ActualHeight / O.AllMenus[CurrentMenu].Count,
                    Margin = new Thickness(0, height, 0, 0)
                };
                Grid.SetColumn(highlighted, 0);
                Grid.SetRow(highlighted, 1);

                // Put highlight box behind text, add to layout
                System.Windows.Controls.Panel.SetZIndex(highlighted, 1);
                O.Layout.Children.Add(highlighted);
            });
            
            AddMenuOptions(yRes,index);
        }
    }

    private void AddMenuOptions(double yRes, int index)
    {
        // Adds all menu options to the menu
        foreach (var item in O.AllMenus[CurrentMenu])
        {
            string? text, value = string.Empty, description = string.Empty;
            SolidColorBrush fColour;
            if (index == _selectedOptionIndex)
            {
                text = $"[{item.Name}]";
                fColour = item.IsEnabled ? Brushes.Green : Brushes.DarkOliveGreen;
                description = item.Description;
            }
            else
            {
                text = $"{item.Name}";
                fColour = item.IsEnabled ? Brushes.White : Brushes.DimGray;
            }

            value = item.Type switch
            {
                MenuButton => ">",
                Float => $"<{item.Value:0.00000}>",
                Int => $"<{item.Value}>",
                Selection => $"<{item.Selections?[(int)item.Value]}>",
                Bool when (bool)item.Value => "[X]",
                Bool when (bool)item.Value == false => "[ ]",
                _ => value
            };

            O.Dispatcher.BeginInvoke((Action<int>)delegate (int idx)
            {
                try
                {
                    if (item.Type != SubHeader)
                    {
                        O.OptionsBlock.Inlines.Add(new Run(text)
                        {
                            Foreground = fColour,
                            FontSize = yRes / (5d / FontSize * 45d)
                        });

                        O.ValueBlock.Inlines.Add(new Run(value)
                        {
                            Foreground = fColour,
                            FontSize = yRes / (5d / FontSize * 45d)
                        });

                        if (description != string.Empty && idx == _selectedOptionIndex)
                        {
                            O.DescriptionBlock.Text = description;
                            O.DescriptionBlock.FontSize = yRes / (5d / FontSize * 45d);
                            O.DescriptionBlock.Foreground = Brushes.White;
                        }
                        else if (idx == _selectedOptionIndex)
                        {
                            O.DescriptionBlock.Text = string.Empty;
                        }
                    }
                    else
                    {
                        O.OptionsBlock.Inlines.Add(new InlineUIContainer
                        {
                            Child = new TextBlock
                            {
                                Text = text,
                                Foreground = fColour,
                                FontSize = yRes / (5d / FontSize * 45d),
                                Width = O.Width - 10,
                                HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                                TextAlignment = TextAlignment.Center
                            }
                        });
                        O.ValueBlock.Inlines.Add(new Run("") { FontSize = yRes / (5d / FontSize * 45d) });
                    }
                }
                catch
                {
                    O.OverlayToggle(false);
                    Task.Delay(25).Wait();
                    O.OverlayToggle(true);
                }
            }, index);

            if (O.AllMenus[CurrentMenu].IndexOf(item) != O.AllMenus[CurrentMenu].Count - 1)
            {
                O.Dispatcher.BeginInvoke((Action)delegate
                {
                    O.OptionsBlock.Inlines.Add("\n"); 
                    O.ValueBlock.Inlines.Add("\n");
                });
            }
            index++;
        }
    }
    
    // Handles the input
    public void KeyHandler(CancellationToken ct)
    {
        while (true)
        {
            Task.Delay(10, ct).Wait(ct);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            var isGameFocused = Mw.Gvp.Process.MainWindowHandle == GetForegroundWindow();

            if (!isGameFocused)
            {
                continue;
            }

            UpdateKeyStates();
            
            var currentOption = O.AllMenus[CurrentMenu][_selectedOptionIndex];
            
            if (GetAsyncKeyState(Confirm) is 1 or short.MinValue && currentOption.IsEnabled)
            {
                switch (currentOption.Type)
                {
                    case MenuButton:
                    {
                        _levelIndex++;
                        
                        var nameSplit = currentOption.Name.Split(' ', '/', '[', ']', '&');
                        CurrentMenu = string.Empty;
                            
                        foreach (var item in nameSplit)
                        {
                            CurrentMenu += char.ToUpper(item[0]) + item[1..];
                        }

                        CurrentMenu += "Options";
                        _history.Add(_levelIndex, CurrentMenu);
                        _selectedOptionIndex = 0;
                        break;
                    }
                    case Bool:
                    {
                        currentOption.Value = !(bool)currentOption.Value;
                        break;
                    }
                    case OptionType.Button:
                    {
                        ((Action)currentOption.Value)();
                        break;
                    }
                }

                while (GetAsyncKeyState(Confirm) is 1 or short.MinValue)
                {
                    Task.Delay(10, ct).Wait(ct);
                }
            }
            else if (GetAsyncKeyState(Leave) is 1 or short.MinValue)
            {
                if (_levelIndex == 0)
                {
                    continue;
                }
                    
                _levelIndex--;
                CurrentMenu = _history[_levelIndex];
                _history.Remove(_levelIndex + 1);
                _selectedOptionIndex = 0;
                while (GetAsyncKeyState(Leave) is 1 or short.MinValue)
                {
                    Task.Delay(10, ct).Wait(ct);
                }
            }
            else if (GetAsyncKeyState(OverlayVisibility) is 1 or short.MinValue)
            {
                    
                if (O.Visibility == Visible)
                {
                    O.Dispatcher.Invoke(delegate { O.Hide(); });
                }
                else
                {
                    O.Dispatcher.Invoke(delegate { O.Show(); });
                }

                _hidden = !_hidden;
                while (GetAsyncKeyState(OverlayVisibility) is 1 or short.MinValue)
                {
                    Task.Delay(10, ct).Wait(ct);
                }
            }
        }
    }

    private void UpdateKeyStates()
    {
        UpdateKeyState(Down, ref _downKeyDown);
        UpdateKeyState(Up, ref _upKeyDown);
        UpdateKeyState(Left, ref _leftKeyDown);
        UpdateKeyState(Right, ref _rightKeyDown);
        UpdateKeyState(RapidAdjust, ref _rapidKeyDown);
    }
    
    private static void UpdateKeyState(Keys key, ref bool keyDownBool)
    {
        var isKeyPressed = (GetAsyncKeyState(key) & (1 | short.MinValue)) != 0;

        keyDownBool = isKeyPressed switch
        {
            true when !keyDownBool => true,
            false when keyDownBool => false,
            _ => keyDownBool
        };
    }
    
    public void ChangeSelection(CancellationToken ct)
    {
        while (true)
        {
            Task.Delay(10, ct).Wait(ct);
                
            if (ct.IsCancellationRequested)
            {
                break;
            }

            if (_hidden)
            {
                continue;
            }
            
            var isGameFocused = Mw.Gvp.Process.MainWindowHandle == GetForegroundWindow();

            if (!isGameFocused)
            {
                continue;
            }
            
            if (_downKeyDown && isGameFocused)
            {
                void Down()
                {
                    _selectedOptionIndex++;
                    if (_selectedOptionIndex > O.AllMenus[CurrentMenu].Count - 1)
                        _selectedOptionIndex = 0;
                }
                Down();

                var timer = new Timer();
                timer.Interval = 150;
                timer.Tick += delegate
                {
                    Down();
                    Task.Delay(5, ct).Wait(ct);
                };
                
                O.Dispatcher.Invoke(delegate
                {
                    timer.Start();
                });
                
                while (_downKeyDown)
                {
                    Task.Delay(1, ct).Wait(ct);
                }
                
                O.Dispatcher.Invoke(delegate
                {
                    timer.Dispose();
                });
            }
            else if (_upKeyDown && isGameFocused)
            {
                void Up()
                {
                    _selectedOptionIndex--;
                    if (_selectedOptionIndex < 0)
                        _selectedOptionIndex = O.AllMenus[CurrentMenu].Count - 1;
                }
                Up();

                var timer = new Timer();
                timer.Interval = 150;
                timer.Tick += delegate
                { 
                    Up();
                    Task.Delay(5, ct).Wait(ct);
                };
                
                O.Dispatcher.Invoke(delegate
                {
                    timer.Start();
                });
                
                while (_upKeyDown)
                {
                    Task.Delay(1, ct).Wait(ct);
                }
                
                O.Dispatcher.Invoke(delegate
                {
                    timer.Dispose();
                });
            }
        }
    }

    public void ChangeValue(CancellationToken ct)
    {
        while (true)
        {
            Task.Delay(_rapidKeyDown ? 5 : 100, ct).Wait(ct);

            if (ct.IsCancellationRequested)
            {
                return;
            }

            var isGameFocused = Mw.Gvp.Process.MainWindowHandle == GetForegroundWindow();
            var currentOption = O.AllMenus[CurrentMenu][_selectedOptionIndex];

            if (_rightKeyDown && currentOption.IsEnabled && isGameFocused)
            {
                var min = currentOption.Min;
                var max = currentOption.Max;
                var value = currentOption.Value;
                
                currentOption.Value = currentOption.Type switch
                {
                    // Value is more than 0 so set to the first selection
                    Selection when (int)currentOption.Value >= currentOption.Selections!.Length - 1 => 0,
                    
                    // Value is more than max, set to min if exists
                    Float when min != null && max != null && (float)value >= (float)max => currentOption.Value = min,
                    Int when min != null && max != null && (int)value >= (int)max => currentOption.Value = min,
                    
                    // Value is more than max, set to max bc min doesnt exist
                    Float when max != null && (float)value >= (float)max => currentOption.Value = max,
                    Int when max != null && (int)value >= (int)max => currentOption.Value = max,
                    
                    // Default increment
                    Float => ToSingle(Math.Round((float)currentOption.Value + (float)(currentOption.Interval ?? 0.1f), 1)),
                    Int => (int)currentOption.Value + (int)(currentOption.Interval ?? 1),
                    Selection => (int)currentOption.Value + 1,
                    
                    Bool => currentOption.Value = true,
                    _ => currentOption.Value
                };
            }
            else if (_leftKeyDown && currentOption.IsEnabled && isGameFocused)
            {
                var min = currentOption.Min;
                var max = currentOption.Max;
                var value = currentOption.Value;
                
                currentOption.Value = currentOption.Type switch
                {
                    // Value is less than 0 so set to the last selection
                    Selection when (int)currentOption.Value <= 0 => currentOption.Selections!.Length - 1,
                    
                    // Value is less than min, set to max if exists
                    Float when min != null && max != null && (float)value <= (float)min => currentOption.Value = max,
                    Int when min != null && max != null && (int)value <= (int)min => currentOption.Value = max,
                    
                    // Value is less than min, set to min bc max doesn't exist
                    Float when min != null && (float)value <= (float)min => currentOption.Value = min,
                    Int when min != null && (int)value <= (int)min => currentOption.Value = min,
                    
                    // Default decrement
                    Float => ToSingle(Math.Round((float)currentOption.Value - (float)(currentOption.Interval ?? 0.1f), 1)),
                    Int => (int)currentOption.Value - (int)(currentOption.Interval ?? 1),
                    Selection => (int)currentOption.Value - 1,
                    
                    Bool => currentOption.Value = false,
                    _ => currentOption.Value
                };
            }
        }
    }

    // Configs and enables blur
    internal static void EnableBlur()
    {
        var windowHelper = new WindowInteropHelper(O!);

        var accent = new AccentPolicy
        {
            AccentState = AccentState.AccentEnableBlurBehind
        };

        var accentStructSize = Marshal.SizeOf(accent);

        var accentPtr = Marshal.AllocHGlobal(accentStructSize);
        Marshal.StructureToPtr(accent, accentPtr, false);

        var data = new WindowCompositionAttributeData
        {
            Attribute = WindowCompositionAttribute.WcaAccentPolicy,
            SizeOfData = accentStructSize,
            Data = accentPtr
        };

        SetWindowCompositionAttribute(windowHelper.Handle, ref data);
            
        Marshal.FreeHGlobal(accentPtr);
    }
}