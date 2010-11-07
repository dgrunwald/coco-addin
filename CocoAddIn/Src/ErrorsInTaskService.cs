// Coco/R Custom Tool - Coco/R integration into SharpDevelop
// Copyright (C) 2007  Daniel Grunwald
// 
// This program is free software; you can redistribute it and/or
// modify it under the terms of the GNU General Public License
// as published by the Free Software Foundation; either version 2
// of the License, or (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program; if not, write to the Free Software
// Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.

using System;
using System.IO;
using System.Linq;

using at.jku.ssw.Coco;
using ICSharpCode.Core;
using ICSharpCode.SharpDevelop;
using ICSharpCode.SharpDevelop.Gui;
using ICSharpCode.SharpDevelop.Project;

namespace Grunwald.CocoAddIn
{
	sealed class ErrorsInTaskService : Errors
	{
		#if SD4
		readonly FileName fileName;
		#else
		readonly string fileName;
		#endif
		
		public ErrorsInTaskService(string fileName)
		{
			#if SD4
			this.fileName = FileName.Create(fileName);
			#else
			this.fileName = fileName;
			#endif
		}
		
		public static void ClearAllErrors()
		{
			foreach (Task task in TaskService.Tasks.ToArray()) {
				if ((task.Tag as Type) == typeof(ErrorsInTaskService))
					TaskService.Remove(task);
			}
		}
		
		public override void WriteError(int line, int col, string message)
		{
			base.WriteError(line, col, message);
			TaskService.Add(new Task(fileName, message, col, line, TaskType.Error) { Tag = typeof(ErrorsInTaskService) });
		}
		
		public override void Warning(int line, int col, string s)
		{
			base.Warning(line, col, s);
			TaskService.Add(new Task(fileName, s, col, line, TaskType.Warning) { Tag = typeof(ErrorsInTaskService) });
		}
		
		public override void Warning(string s)
		{
			base.Warning(s);
			TaskService.Add(new Task(fileName, s, -1, -1, TaskType.Warning) { Tag = typeof(ErrorsInTaskService) });
		}
	}
}
