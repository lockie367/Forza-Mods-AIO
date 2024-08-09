using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Forza_Mods_AIO.Models;
using static Forza_Mods_AIO.Resources.Cheats;

namespace Forza_Mods_AIO.ViewModels.Pages;

public partial class AutoshowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _uiElementsEnabled = true;
    ForzaHorizon5
    private static Cheats.ForzaHorizon5.Sql SqlFh5 => GetClass<Cheats..Sql>();
    private static Cheats.ForzaHorizon4.Sql SqlFh4 => GetClass<Cheats.ForzaHorizon4.Sql>();

    [RelayCommand]
    private async Task ExecuteSql(object parameter)
    {
        if (parameter is not string sParam)
        {
            return;
        }
        
        UiElementsEnabled = false;
        await Query(sParam);
        UiElementsEnabled = true;
    }

    private static async Task Query(string command)
    {
        switch (GameVerPlat.GetInstance().Type)
        {
            case GameVerPlat.GameType.Fh4:
            {
                if (!SqlFh4.WereScansSuccessful)
                {
                    await SqlFh4.SqlExecAobScan();
                }

                if (!SqlFh4.WereScansSuccessful)
                {
                    goto SkipQuerying;
                }
        
                await Task.Run(() => SqlFh4.Query(command));
                SkipQuerying:
                break;
            }
            case GameVerPlat.GameType.Fh5:
            {
                if (!SqlFh5.WereScansSuccessful)
                {
                    await SqlFh5.SqlExecAobScan();
                }

                if (!SqlFh5.WereScansSuccessful)
                {
                    goto SkipQuerying;
                }
        
                await Task.Run(() => SqlFh5.Query(command));
                SkipQuerying:
                break;
            }
            case GameVerPlat.GameType.None:
            default:
            {
                throw new IndexOutOfRangeException();
            }
        }
    }
}