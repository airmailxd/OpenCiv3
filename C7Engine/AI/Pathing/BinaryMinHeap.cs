using System;
using System.Collections.Generic;

namespace C7Engine.Pathing {
	/**
	 * https://en.wikipedia.org/wiki/Binary_heap
	 *
	 * Not used by the pathfinder, which uses PriorityQueue (see
	 * PathSearchContext); kept as a general purpose utility.
	 */
	public class BinaryMinHeap<TValue> where TValue : IComparable<TValue> {
		private readonly List<TValue> data = new List<TValue>();

		public int count { get => data.Count; }

		// average O(1), worst case O(log N)
		public void insert(TValue v) {
			data.Add(v);
			siftUp(data.Count - 1);
		}

		// extract the smallest value, O(log N)
		public TValue extract() {
			TValue result = data[0];
			int last = data.Count - 1;
			data[0] = data[last];
			data.RemoveAt(last);
			if (data.Count > 0) {
				siftDown(0);
			}
			return result;
		}

		private void siftUp(int childIndex) {
			TValue item = data[childIndex];
			while (childIndex > 0) {
				int parentIndex = getParentIndex(childIndex);
				TValue parent = data[parentIndex];
				if (parent.CompareTo(item) <= 0) {
					break;
				}
				data[childIndex] = parent;
				childIndex = parentIndex;
			}
			data[childIndex] = item;
		}

		private void siftDown(int parentIndex) {
			int n = data.Count;
			TValue item = data[parentIndex];
			while (true) {
				int leftChild = getLeftChild(parentIndex);
				if (leftChild >= n) {
					break;
				}
				int rightChild = leftChild + 1;
				int topChildIndex = (rightChild < n && data[rightChild].CompareTo(data[leftChild]) < 0) ? rightChild : leftChild;
				TValue topChild = data[topChildIndex];
				if (item.CompareTo(topChild) <= 0) {
					break;
				}
				data[parentIndex] = topChild;
				parentIndex = topChildIndex;
			}
			data[parentIndex] = item;
		}

		// 0 -> 0;  1,2 -> 0;  3,4 -> 1; 5,6 -> 2;
		private static int getParentIndex(int index) {
			return (index + 1) / 2 - 1;
		}

		// 0 -> 1; 1 -> 3; 2 -> 5; 3 -> 7
		private static int getLeftChild(int index) {
			return (index + 1) * 2 - 1;
		}
	}
}
