/*
  KeePass Password Safe - The Open-Source Password Manager
  Copyright (C) 2003-2026 Dominik Reichl <dominik.reichl@t-online.de>

  This program is free software; you can redistribute it and/or modify
  it under the terms of the GNU General Public License as published by
  the Free Software Foundation; either version 2 of the License, or
  (at your option) any later version.
*/

namespace KeePass.Core.Projections
{
	/// <summary>
	/// Maps a domain object of type <typeparamref name="TSource"/> to an
	/// immutable read-only projection of type <typeparamref name="TProjection"/>.
	///
	/// <para>Implementations must never mutate the source object and must
	/// produce a snapshot that is independent from the source — subsequent
	/// changes to the source must not affect the projection.</para>
	/// </summary>
	/// <typeparam name="TSource">The domain type (e.g. PwEntry, PwGroup).</typeparam>
	/// <typeparam name="TProjection">The immutable projection type.</typeparam>
	public interface IProjectionMapper<TSource, TProjection>
		where TSource : class
		where TProjection : class
	{
		/// <summary>
		/// Creates and returns an immutable projection from the given domain object.
		/// </summary>
		/// <param name="source">The domain object to project. Must not be null.</param>
		/// <returns>An immutable snapshot of <paramref name="source"/>.</returns>
		TProjection FromDomain(TSource source);
	}
}
