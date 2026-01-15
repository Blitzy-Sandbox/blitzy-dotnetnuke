'
' DotNetNuke® - http://www.dotnetnuke.com
' Copyright (c) 2002-2008
' by DotNetNuke Corporation
'
' Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated 
' documentation files (the "Software"), to deal in the Software without restriction, including without limitation 
' the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and 
' to permit persons to whom the Software is furnished to do so, subject to the following conditions:
'
' The above copyright notice and this permission notice shall be included in all copies or substantial portions 
' of the Software.
'
' THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED 
' TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL 
' THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF 
' CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER 
' DEALINGS IN THE SOFTWARE.
'

Imports System
Imports System.Collections
Imports DotNetNuke.Services.Localization


Namespace DotNetNuke.Modules.Admin.ResourceInstaller

	Public Class PaLogger
		Inherits ResourceInstallerBase

		Private m_Logs As ArrayList
		Private m_Valid As Boolean
		Private _NormalClass As String
		Private _HighlightClass As String
		Private _ErrorClass As String

		Public Sub New()
			m_Logs = New ArrayList
			m_Valid = True
		End Sub		  'New

		Public Sub AddInfo(ByVal info As String)
			m_Logs.Add(New PaLogEntry(PaLogType.Info, info))
		End Sub		  'AddInfo


		Public Sub AddWarning(ByVal warning As String)
			m_Logs.Add(New PaLogEntry(PaLogType.Warning, warning))
		End Sub		  'AddWarning


		Public Sub AddFailure(ByVal failure As String)
			m_Logs.Add(New PaLogEntry(PaLogType.Failure, failure))
			m_Valid = False
		End Sub		  'AddFailure


		Public Sub Add(ByVal ex As Exception)
			AddFailure((EXCEPTION + ex.ToString()))
		End Sub		  'Add


		Public Sub StartJob(ByVal job As String)
			m_Logs.Add(New PaLogEntry(PaLogType.StartJob, job))
		End Sub		  'StartJob


		Public Sub EndJob(ByVal job As String)
			m_Logs.Add(New PaLogEntry(PaLogType.EndJob, job))
		End Sub		  'EndJob

		Public ReadOnly Property Logs() As ArrayList
			Get
				Return m_Logs
			End Get
		End Property

		Public ReadOnly Property Valid() As Boolean
			Get
				Return m_Valid
			End Get
		End Property

		Public Property ErrorClass() As String
			Get
				If _ErrorClass = "" Then
					_ErrorClass = "NormalRed"
				End If
				Return _ErrorClass
			End Get
			Set(ByVal Value As String)
				_ErrorClass = Value
			End Set
		End Property


		Public Property HighlightClass() As String
			Get
				If _HighlightClass = "" Then
					_HighlightClass = "NormalBold"
				End If
				Return _HighlightClass
			End Get
			Set(ByVal Value As String)
				_HighlightClass = Value
			End Set
		End Property

		Public Property NormalClass() As String
			Get
				If _NormalClass = "" Then
					_NormalClass = "Normal"
				End If
				Return _NormalClass
			End Get
			Set(ByVal Value As String)
				_NormalClass = Value
			End Set
		End Property

		''' -----------------------------------------------------------------------------
		''' <summary>
		''' GetLogsTable formats log entries in an HtmlTable
		''' </summary>
		''' <history>
		'''   [jbrinkman] 24/11/2004  Created new method.  Moved from WebUpload.ascx.vb
		''' </history>
		''' -----------------------------------------------------------------------------
		Public Function GetLogsTable() As HtmlTable
			Dim arrayTable As New HtmlTable

			Dim LogEntry As DotNetNuke.Modules.Admin.ResourceInstaller.PaLogEntry

			For Each LogEntry In Logs
				Dim tr As New HtmlTableRow
				Dim tdType As New HtmlTableCell
                tdType.InnerText = Localization.GetString("LOG.PALogger." & LogEntry.Type.ToString)
				Dim tdDescription As New HtmlTableCell
				tdDescription.InnerText = LogEntry.Description
				tr.Cells.Add(tdType)
				tr.Cells.Add(tdDescription)
				Select Case LogEntry.Type
					Case PaLogType.Failure, PaLogType.Warning
						tr.Attributes.Add("class", ErrorClass)
					Case PaLogType.StartJob, PaLogType.EndJob
						tr.Attributes.Add("class", HighlightClass)
					Case PaLogType.Info
						tr.Attributes.Add("class", NormalClass)
				End Select
				arrayTable.Rows.Add(tr)
				If LogEntry.Type = PaLogType.EndJob Then
					Dim SpaceTR As New HtmlTableRow
					Dim SpaceTD As New HtmlTableCell
					SpaceTD.ColSpan = 2
					SpaceTD.InnerHtml = "&nbsp;"
					SpaceTR.Cells.Add(SpaceTD)
					arrayTable.Rows.Add(SpaceTR)
				End If
			Next

			Return arrayTable
		End Function

	End Class	'PaLogger
End Namespace
