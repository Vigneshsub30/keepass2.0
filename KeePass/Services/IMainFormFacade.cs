using KeePassLib;

namespace KeePass.Services
{
	/// <summary>
	/// Minimal abstraction over <c>MainForm</c> that exposes only the
	/// operations required by <c>DefaultPluginHost</c>.  Implementing this
	/// interface on <c>MainForm</c> breaks the direct dependency so that
	/// <c>DefaultPluginHost</c> no longer needs a concrete <c>MainForm</c>
	/// reference in its field.
	/// </summary>
	/// <remarks>
	/// The interface is intentionally narrow — it covers only the members
	/// that <c>DefaultPluginHost</c> actually calls on MainForm.  Future
	/// consumers may extend it, but every addition must be justified by an
	/// active call site to avoid creating a new God Object.
	/// </remarks>
	public interface IMainFormFacade
	{
		/// <summary>
		/// The currently active <see cref="PwDatabase"/>, or <c>null</c> if
		/// no database is open.
		/// </summary>
		PwDatabase? ActiveDatabase { get; }
	}
}
