using UnityEngine;

namespace xNode
{
    using Sirenix.OdinInspector;

    public abstract class ConnectionNode<TConnection, T> : Node where TConnection : NodeConnection<T>
    {
        [SerializeField] [Output] private TConnection output;

        protected abstract T ConnectionSource { get; }

        public override object GetValue(NodePort port)
        {
            return port.fieldName == nameof(this.output)
                ? this.output
                : base.GetValue(port);
        }

        public override void OnCreateConnection(NodePort @from, NodePort to)
        {
            base.OnCreateConnection(@from, to);
            this.UpdateConnections();
        }

        public override void OnRemoveConnection(NodePort port)
        {
            base.OnRemoveConnection(port);
            this.UpdateConnections();
        }

        protected override void Init() => this.UpdateConnections();

        protected virtual void UpdateConnections()
        {
            if (this.output != null)
                this.output.Source = this.ConnectionSource;
        }

        protected abstract void RenderPreview();

        private void OnValidate() => this.UpdatePreview();

        [Button]
        private void UpdatePreview()
        {
            this.UpdateConnections();
            this.RenderPreview();
        }
    }
}