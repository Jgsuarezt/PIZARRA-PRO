using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Ink;
using PizarraPro.ViewModels;

namespace PizarraPro.Services;

public interface IUndoableAction
{
    void Undo();
    void Redo();
}

public class UndoRedoManager
{
    private readonly Stack<IUndoableAction> _undoStack = new();
    private readonly Stack<IUndoableAction> _redoStack = new();

    public bool IsApplying { get; private set; }

    public event EventHandler? StateChanged;

    public bool CanUndo => _undoStack.Count > 0;

    public bool CanRedo => _redoStack.Count > 0;

    public void Push(IUndoableAction action)
    {
        if (IsApplying) return;
        _undoStack.Push(action);
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (_undoStack.Count == 0) return;
        var action = _undoStack.Pop();
        IsApplying = true;
        try { action.Undo(); }
        finally { IsApplying = false; }
        _redoStack.Push(action);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (_redoStack.Count == 0) return;
        var action = _redoStack.Pop();
        IsApplying = true;
        try { action.Redo(); }
        finally { IsApplying = false; }
        _undoStack.Push(action);
        StateChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undoStack.Clear();
        _redoStack.Clear();
        StateChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>Deshace/rehace un cambio atómico (añadido/eliminado) en una colección de trazos.</summary>
public class StrokesChangedAction : IUndoableAction
{
    private readonly StrokeCollection _target;
    private readonly StrokeCollection _added;
    private readonly StrokeCollection _removed;

    public StrokesChangedAction(StrokeCollection target, StrokeCollection added, StrokeCollection removed)
    {
        _target = target;
        _added = added;
        _removed = removed;
    }

    public void Undo()
    {
        if (_added.Count > 0) _target.Remove(_added);
        if (_removed.Count > 0) _target.Add(_removed);
    }

    public void Redo()
    {
        if (_removed.Count > 0) _target.Remove(_removed);
        if (_added.Count > 0) _target.Add(_added);
    }
}

public class TextElementAddedAction : IUndoableAction
{
    private readonly ObservableCollection<TextElementViewModel> _collection;
    private readonly TextElementViewModel _element;

    public TextElementAddedAction(ObservableCollection<TextElementViewModel> collection, TextElementViewModel element)
    {
        _collection = collection;
        _element = element;
    }

    public void Undo() => _collection.Remove(_element);

    public void Redo() => _collection.Add(_element);
}

public class TextElementRemovedAction : IUndoableAction
{
    private readonly ObservableCollection<TextElementViewModel> _collection;
    private readonly TextElementViewModel _element;
    private readonly int _index;

    public TextElementRemovedAction(ObservableCollection<TextElementViewModel> collection, TextElementViewModel element, int index)
    {
        _collection = collection;
        _element = element;
        _index = index;
    }

    public void Undo() => _collection.Insert(Math.Min(_index, _collection.Count), _element);

    public void Redo() => _collection.Remove(_element);
}
