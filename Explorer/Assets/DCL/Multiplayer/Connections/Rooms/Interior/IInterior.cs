using DCL.Utilities.Extensions;

namespace DCL.Multiplayer.Connections.Rooms.Interior
{
    /// <summary>
    ///     Provides a way to assign a value to immutable object implements a interior mutability pattern from rust
    ///     https://doc.rust-lang.org/book/ch15-05-interior-mutability.html
    /// </summary>
    public interface IInterior<T>
    {
        void Assign(T value, out T? previous);
    }

    public static class InteriorExtensions
    {
        public static void Assign<T>(this IInterior<T> interior, T value)
        {
            interior.Assign(value, out _);
        }

        public static T EnsureAssigned<T>(this T? value) =>
            value.EnsureNotNull("Interior value is not assigned");
    }
}
