using Microsoft.Extensions.Time.Testing;
using YappyNotes.Core;
using YappyNotes.TestKit;
using YappyNotes.ViewModels;

namespace YappyNotes.ViewModels.Tests;

public class NoteImageTests
{
    private readonly FakeTimeProvider _clock = new();
    private readonly InMemoryNoteRepository _repository = new();
    private readonly CountingNoteRepository _counting;
    private readonly NoteService _notes;
    private readonly AutoSaveService _autoSave;

    public NoteImageTests()
    {
        _counting = new CountingNoteRepository(_repository);
        _notes = new NoteService(_counting, _clock);
        _autoSave = new AutoSaveService(_notes, _clock, TimeSpan.FromMilliseconds(750));
    }

    private async Task<NoteViewModel> OpenAsync(string content, IImageFilePicker picker)
    {
        var note = await _notes.CreateAsync();
        note.Content = content;
        return new NoteViewModel(note, _notes, _autoSave, new FakeWindowManager(), images: picker);
    }

    [Fact]
    public async Task InsertImage_WithAChosenFile_UpdatesContentOnce()
    {
        var picker = new ImagePicker(new Uri("file:///tmp/meu%20diagrama.png"));
        var note = await OpenAsync("Olá!  Tchau!", picker);

        var edit = await note.AddImageAsync(5, 5);
        _clock.Advance(TimeSpan.FromMilliseconds(750));
        await _autoSave.WhenIdleAsync();

        Assert.NotNull(edit);
        Assert.Equal("Olá! \n![meu diagrama](file:///tmp/meu%20diagrama.png)\n Tchau!", note.Content);
        Assert.Equal(1, _counting.Updates);
        Assert.Equal(note.Content, (await _repository.GetByIdAsync(note.Id, TestContext.Current.CancellationToken))!.Content);
        Assert.Equal(MarkdownBlockKind.Image, note.Lines[1].Block.Kind);
    }

    [Fact]
    public async Task AddImage_WhenPickerIsCancelled_DoesNotEditOrSave()
    {
        var note = await OpenAsync("Olá!  Tchau!", new ImagePicker(null));

        var edit = await note.AddImageAsync(5, 7);
        _clock.Advance(TimeSpan.FromMilliseconds(750));
        await _autoSave.WhenIdleAsync();

        Assert.Null(edit);
        Assert.Equal("Olá!  Tchau!", note.Content);
        Assert.Equal(0, _counting.Updates);
    }

    [Fact]
    public async Task AddImage_WhenPickerReturnsRemoteUri_DoesNotEditOrSave()
    {
        var note = await OpenAsync("Olá!  Tchau!", new ImagePicker(new Uri("https://example.com/diagram.png")));

        var edit = await note.AddImageAsync(5, 7);
        _clock.Advance(TimeSpan.FromMilliseconds(750));
        await _autoSave.WhenIdleAsync();

        Assert.Null(edit);
        Assert.Equal("Olá!  Tchau!", note.Content);
        Assert.Equal(0, _counting.Updates);
    }

    [Fact]
    public async Task PressAsync_OnImage_OpensEditorAtImageMarkdownStart()
    {
        const string content = "before\n![diagram](file:///tmp/diagram.png)\nafter";
        var links = new FakeLinkLauncher();
        var stored = await _notes.CreateAsync(TestContext.Current.CancellationToken);
        stored.Content = content;
        var note = new NoteViewModel(stored, _notes, _autoSave, new FakeWindowManager(), links: links);

        await note.PressAsync(note.Lines[1], 0);
        _clock.Advance(TimeSpan.FromMilliseconds(750));
        await _autoSave.WhenIdleAsync();

        Assert.True(note.IsEditing);
        Assert.Equal(content.IndexOf("![", StringComparison.Ordinal), note.EditCaret);
        Assert.Equal(content, note.Content);
        Assert.Empty(links.Opened);
        Assert.Equal(0, _counting.Updates);
    }

    private sealed class ImagePicker(Uri? chosen) : IImageFilePicker
    {
        public Task<Uri?> PickAsync() => Task.FromResult(chosen);
    }
}
