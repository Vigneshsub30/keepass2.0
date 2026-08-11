using System;
using System.Xml;

namespace KeePass.Core.DataExchange
{
	/// <summary>
	/// A delegating <see cref="XmlReader"/> that tracks element nesting depth
	/// and throws <see cref="ImportValidationException"/> when the depth
	/// exceeds <see cref="ImportValidationOptions.MaxXmlDepth"/>.
	/// </summary>
	public sealed class DepthLimitingXmlReader : XmlReader
	{
		private readonly XmlReader _inner;
		private readonly int _maxDepth;
		private int _depth;

		/// <param name="inner">Underlying XML reader. Not closed when
		///   this reader is disposed.</param>
		/// <param name="maxDepth">Maximum element nesting depth allowed.</param>
		public DepthLimitingXmlReader(XmlReader inner, int maxDepth)
		{
			if(inner is null)    throw new ArgumentNullException(nameof(inner));
			if(maxDepth < 0)     throw new ArgumentOutOfRangeException(nameof(maxDepth));
			_inner    = inner;
			_maxDepth = maxDepth;
		}

		// ── XmlReader delegated properties ────────────────────────── //

		public override string              BaseURI      => _inner.BaseURI;
		public override int                 AttributeCount => _inner.AttributeCount;
		public override bool                CanResolveEntity => _inner.CanResolveEntity;
		public override int                 Depth        => _inner.Depth;
		public override bool                EOF          => _inner.EOF;
		public override bool                HasValue     => _inner.HasValue;
		public override bool                IsEmptyElement => _inner.IsEmptyElement;
		public override string              LocalName    => _inner.LocalName;
		public override string              Name         => _inner.Name;
		public override string              NamespaceURI => _inner.NamespaceURI;
		public override XmlNameTable        NameTable    => _inner.NameTable;
		public override XmlNodeType         NodeType     => _inner.NodeType;
		public override string              Prefix       => _inner.Prefix;
		public override ReadState           ReadState    => _inner.ReadState;
		public override string              Value        => _inner.Value;
		public override string              XmlLang     => _inner.XmlLang;
		public override XmlSpace            XmlSpace     => _inner.XmlSpace;

		// ── Read (depth-tracking) ─────────────────────────────────── //

		public override bool Read()
		{
			bool result = _inner.Read();
			if(result)
			{
				if(_inner.NodeType == XmlNodeType.Element && !_inner.IsEmptyElement)
					++_depth;
				else if(_inner.NodeType == XmlNodeType.EndElement)
					--_depth;

				if(_depth > _maxDepth)
					throw new ImportValidationException(
						nameof(ImportValidationOptions.MaxXmlDepth),
						_maxDepth,
						_depth);
			}
			return result;
		}

		// ── Delegated attribute / navigation ─────────────────────── //

		public override string? GetAttribute(int i) => _inner.GetAttribute(i);
		public override string? GetAttribute(string name) => _inner.GetAttribute(name);
		public override string? GetAttribute(string name, string? namespaceURI)
			=> _inner.GetAttribute(name, namespaceURI);

		public override bool MoveToAttribute(string name) => _inner.MoveToAttribute(name);
		public override bool MoveToAttribute(string name, string? ns)
			=> _inner.MoveToAttribute(name, ns);
		public override bool MoveToElement()      => _inner.MoveToElement();
		public override bool MoveToFirstAttribute() => _inner.MoveToFirstAttribute();
		public override bool MoveToNextAttribute()  => _inner.MoveToNextAttribute();

		public override string? LookupNamespace(string prefix)
			=> _inner.LookupNamespace(prefix);

		public override bool ReadAttributeValue() => _inner.ReadAttributeValue();

		public override void ResolveEntity() => _inner.ResolveEntity();

		protected override void Dispose(bool disposing)
		{
			// Intentionally do NOT dispose _inner — the caller owns its lifetime.
			base.Dispose(disposing);
		}
	}
}
