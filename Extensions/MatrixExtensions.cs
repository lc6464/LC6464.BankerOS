using System.Diagnostics.CodeAnalysis;

namespace LC6464.BankerOS.Extensions;

/// <summary>
/// 提供二维矩阵常用转换与复制操作的扩展方法。
/// </summary>
internal static class MatrixExtensions {
	/// <summary>
	/// 为 <see cref="T[,]"/> 提供行列数计算和锯齿数组转换的扩展方法组。
	/// </summary>
	/// <typeparam name="T">矩阵元素的类型</typeparam>
	/// <param name="matrix">当前矩阵</param>
	extension<T>(T[,] matrix) {
		/// <summary>
		/// 获取矩阵的行数。
		/// </summary>
		/// <returns>当前矩阵的行数</returns>
		public int GetRowCount() => matrix.GetLength(0);

		/// <summary>
		/// 获取矩阵的列数。
		/// </summary>
		/// <returns>当前矩阵的列数</returns>
		public int GetColumnCount() => matrix.GetLength(1);

		/// <summary>
		/// 将二维矩阵转换为适合 JSON 序列化的锯齿数组。
		/// </summary>
		/// <returns>与原矩阵内容等价的锯齿数组副本</returns>
		public T[][] ToJaggedMatrix() {
			var rows = matrix.GetRowCount();
			var columns = matrix.GetColumnCount();
			var result = new T[rows][];

			// 逐行创建新数组，确保结果与原二维矩阵完全解耦
			for (var row = 0; row < rows; row++) {
				result[row] = new T[columns];
				for (var column = 0; column < columns; column++) {
					result[row][column] = matrix[row, column];
				}
			}

			return result;
		}

		/// <summary>
		/// 复制矩阵内容到另一个矩阵。
		/// </summary>
		/// <param name="target">目标矩阵，必须与当前矩阵结构一致</param>
		public void CopyTo(T[,] target) {
			var rows = matrix.GetRowCount();
			var columns = matrix.GetColumnCount();

			if (target.GetRowCount() != rows || target.GetColumnCount() != columns) {
				throw new ArgumentException("目标矩阵结构必须与当前矩阵一致。");
			}

			// 手工复制比依赖 Buffer.BlockCopy 更直接，也适用于泛型矩阵元素
			for (var row = 0; row < rows; row++) {
				for (var column = 0; column < columns; column++) {
					target[row, column] = matrix[row, column];
				}
			}
		}
	}

	/// <summary>
	/// 为 <see cref="T[,]"/> 提供从锯齿数组转换的扩展方法。
	/// </summary>
	/// <typeparam name="T">矩阵元素的类型</typeparam>
	extension<T>(T[,]) {
		/// <summary>
		/// 尝试将锯齿数组转换为二维矩阵。
		/// </summary>
		/// <param name="jagged">要转换的锯齿数组</param>
		/// <param name="expectedRowCount">期望的行数</param>
		/// <param name="expectedColumnCount">期望的列数</param>
		/// <param name="matrix">转换成功的二维矩阵</param>
		/// <param name="errorMessage">转换失败时的错误信息</param>
		/// <returns>转换成功返回 true，否则返回 false</returns>
		public static bool TryFromJaggedMatrix(T[][] jagged, int expectedRowCount, int expectedColumnCount, [NotNullWhen(true)] out T[,]? matrix, out string errorMessage) {
			matrix = null;
			errorMessage = string.Empty;

			if (jagged.Length != expectedRowCount) {
				errorMessage = $"锯齿数组的行数必须为 {expectedRowCount}。";
				return false;
			}

			matrix = new T[expectedRowCount, expectedColumnCount];

			// 每一行都要验证列数，再逐项写入二维矩阵，保证还原结果结构严格一致
			for (var row = 0; row < expectedRowCount; row++) {
				if (jagged[row].Length != expectedColumnCount) {
					errorMessage = $"锯齿数组的第 {row} 行列数必须为 {expectedColumnCount}。";
					matrix = null;
					return false;
				}

				for (var column = 0; column < expectedColumnCount; column++) {
					matrix[row, column] = jagged[row][column];
				}
			}

			return true;
		}
	}
}