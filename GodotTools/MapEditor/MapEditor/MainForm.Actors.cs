using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using MapEditor.Models;

namespace MapEditor;

public sealed partial class MainForm
{
    private abstract class Actor<TMessage> : IDisposable
    {
        public bool WorkAsThread { get; set; }

        private readonly BlockingCollection<TMessage> _queue = new(new ConcurrentQueue<TMessage>());
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _worker;

        protected Actor()
        {
            _worker = Task.Run(WorkerLoop);
        }

        public void Send(TMessage message)
        {
            if (!WorkAsThread)
            {
                Process(message);
                return;
            }
            if (!_queue.IsAddingCompleted)
                _queue.Add(message);
        }

        private void WorkerLoop()
        {
            try
            {
                foreach (var msg in _queue.GetConsumingEnumerable(_cts.Token))
                    Process(msg);
            }
            catch (OperationCanceledException)
            {
            }
        }

        protected abstract void Process(TMessage message);

        public void Dispose()
        {
            _queue.CompleteAdding();
            _cts.Cancel();
            try
            {
                _worker.Wait(100);
            }
            catch
            {
            }
            _queue.Dispose();
            _cts.Dispose();
        }
    }

    private enum PortalSyncKind
    {
        Position,
        Target
    }

    private readonly record struct PortalSyncMessage(
        PortalSyncKind Kind,
        string SceneAbsPath,
        string NodePath,
        float X,
        float Y,
        string TargetMapId,
        string TargetPortalId);

    private sealed class PortalSyncActor : Actor<PortalSyncMessage>
    {
        private readonly MainForm _form;

        public PortalSyncActor(MainForm form)
        {
            _form = form;
        }

        protected override void Process(PortalSyncMessage message)
        {
            try
            {
                if (message.Kind == PortalSyncKind.Position)
                    PatchNodePosition(message.SceneAbsPath, message.NodePath, message.X, message.Y);
                else if (message.Kind == PortalSyncKind.Target)
                    PatchPortalTarget(message.SceneAbsPath, message.NodePath, message.TargetMapId, message.TargetPortalId);
                else
                    return;

                if (_form.IsDisposed)
                    return;

                void Refresh()
                {
                    _form._mapGrid.Refresh();
                    _form._canvas.Invalidate();
                    _form._linksGraph.SetData(_form._project, _form._selectedMap, _form._linksList.SelectedItem as MapLink);
                    _form._linksGraph.Invalidate();
                    _form.UpdateStatus();
                }

                if (_form.InvokeRequired)
                    _form.BeginInvoke(Refresh);
                else
                    Refresh();
            }
            catch (Exception ex)
            {
                if (_form.IsDisposed)
                    return;
                void ShowError()
                {
                    MessageBox.Show(_form, ex.Message, "写入失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                if (_form.InvokeRequired)
                    _form.BeginInvoke(ShowError);
                else
                    ShowError();
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _portalSyncActor.Dispose();
        base.Dispose(disposing);
    }
}
