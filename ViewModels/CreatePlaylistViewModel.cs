namespace CameraScriptManager.ViewModels;

public class CreatePlaylistViewModel : ViewModelBase
{
    private string _title = "";
    private string _author = "";
    private string _description = "";
    private string _coverImagePath = "";

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public string Author
    {
        get => _author;
        set => SetProperty(ref _author, value);
    }

    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    public string CoverImagePath
    {
        get => _coverImagePath;
        set => SetProperty(ref _coverImagePath, value);
    }
}
