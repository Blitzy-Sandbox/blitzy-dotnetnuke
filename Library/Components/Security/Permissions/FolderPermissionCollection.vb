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
Imports System.Data
Imports DotNetNuke

Namespace DotNetNuke.Security.Permissions


#Region "FolderPermissionCollection"
	Public Class FolderPermissionCollection
		Inherits CollectionBase

		Public Sub New()
			MyBase.New()
		End Sub
		Public Sub New(ByVal FolderPermissions As ArrayList)
			Dim i As Integer
			For i = 0 To FolderPermissions.Count - 1
				Dim objFolderPerm As Security.Permissions.FolderPermissionInfo = CType(FolderPermissions(i), Security.Permissions.FolderPermissionInfo)
				Add(objFolderPerm)
			Next
		End Sub

		Public Sub New(ByVal FolderPermissions As ArrayList, ByVal FolderPath As String)
			Dim i As Integer
			For i = 0 To FolderPermissions.Count - 1
				Dim objFolderPerm As Security.Permissions.FolderPermissionInfo = CType(FolderPermissions(i), Security.Permissions.FolderPermissionInfo)
				If objFolderPerm.FolderPath = FolderPath Then
					Add(objFolderPerm)
				End If
			Next
		End Sub

		Default Public Property Item(ByVal index As Integer) As FolderPermissionInfo
			Get
				Return CType(List(index), FolderPermissionInfo)
			End Get
			Set(ByVal Value As FolderPermissionInfo)
				List(index) = Value
			End Set
		End Property

		Public Function Add(ByVal value As FolderPermissionInfo) As Integer
			Return List.Add(value)
		End Function


		Public Function IndexOf(ByVal value As FolderPermissionInfo) As Integer
			Return List.IndexOf(value)
		End Function

		Public Sub Insert(ByVal index As Integer, ByVal value As FolderPermissionInfo)
			List.Insert(index, value)
		End Sub

		Public Sub Remove(ByVal value As FolderPermissionInfo)
			List.Remove(value)
		End Sub

		Public Function Contains(ByVal value As FolderPermissionInfo) As Boolean
			Return List.Contains(value)
		End Function

		Public Function CompareTo(ByVal objFolderPermissionCollection As FolderPermissionCollection) As Boolean
			If objFolderPermissionCollection.Count <> Me.Count Then
				Return False
			End If
			innerlist.Sort(New CompareFolderPermissions)
			objFolderPermissionCollection.InnerList.Sort(New CompareFolderPermissions)

			Dim objFolderPermission As FolderPermissionInfo
			Dim i As Integer
			For Each objFolderPermission In objFolderPermissionCollection
				If objFolderPermissionCollection(i).FolderPermissionID <> Me(i).FolderPermissionID _
				Or objFolderPermissionCollection(i).AllowAccess <> Me(i).AllowAccess Then
					Return False
				End If
				i += 1
			Next
			Return True
		End Function

	End Class

	Public Class CompareFolderPermissions
		Implements System.Collections.IComparer

#Region "IComparer Interface"

		Public Function Compare(ByVal x As Object, ByVal y As Object) As Integer Implements System.Collections.IComparer.Compare
			Return CType(x, FolderPermissionInfo).FolderPermissionID.CompareTo(CType(y, FolderPermissionInfo).FolderPermissionID)
		End Function

#End Region

	End Class
#End Region

End Namespace
