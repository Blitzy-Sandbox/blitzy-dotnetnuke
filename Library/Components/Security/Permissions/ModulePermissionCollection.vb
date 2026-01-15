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


#Region "ModulePermissionCollection"
	Public Class ModulePermissionCollection
		Inherits CollectionBase

		Public Sub New()
			MyBase.New()
		End Sub
		Public Sub New(ByVal ModulePermissions As ArrayList)
			Dim i As Integer
			For i = 0 To ModulePermissions.Count - 1
				Dim objModulePerm As Security.Permissions.ModulePermissionInfo = CType(ModulePermissions(i), Security.Permissions.ModulePermissionInfo)
				Add(objModulePerm)
			Next
		End Sub

		Public Sub New(ByVal ModulePermissions As ArrayList, ByVal ModuleID As Integer)
			Dim i As Integer
			For i = 0 To ModulePermissions.Count - 1
				Dim objModulePerm As Security.Permissions.ModulePermissionInfo = CType(ModulePermissions(i), Security.Permissions.ModulePermissionInfo)
				If objModulePerm.ModuleID = ModuleID Then
					Add(objModulePerm)
				End If
			Next
		End Sub

		Default Public Property Item(ByVal index As Integer) As ModulePermissionInfo
			Get
				Return CType(List(index), ModulePermissionInfo)
			End Get
			Set(ByVal Value As ModulePermissionInfo)
				List(index) = Value
			End Set
		End Property

		Public Function Add(ByVal value As ModulePermissionInfo) As Integer
			Return List.Add(value)
		End Function


		Public Function IndexOf(ByVal value As ModulePermissionInfo) As Integer
			Return List.IndexOf(value)
		End Function

		Public Sub Insert(ByVal index As Integer, ByVal value As ModulePermissionInfo)
			List.Insert(index, value)
		End Sub

		Public Sub Remove(ByVal value As ModulePermissionInfo)
			List.Remove(value)
		End Sub

		Public Function Contains(ByVal value As ModulePermissionInfo) As Boolean
			Return List.Contains(value)
		End Function

		Public Function CompareTo(ByVal objModulePermissionCollection As ModulePermissionCollection) As Boolean
			If objModulePermissionCollection.Count <> Me.Count Then
				Return False
			End If
			innerlist.Sort(New CompareModulePermissions)
			objModulePermissionCollection.InnerList.Sort(New CompareModulePermissions)

			Dim objModulePermission As ModulePermissionInfo
			Dim i As Integer
			For Each objModulePermission In objModulePermissionCollection
				If objModulePermissionCollection(i).ModulePermissionID <> Me(i).ModulePermissionID _
				Or objModulePermissionCollection(i).AllowAccess <> Me(i).AllowAccess Then
					Return False
				End If
				i += 1
			Next
			Return True
		End Function

	End Class

	Public Class CompareModulePermissions
		Implements System.Collections.IComparer

#Region "IComparer Interface"

		Public Function Compare(ByVal x As Object, ByVal y As Object) As Integer Implements System.Collections.IComparer.Compare
			Return CType(x, ModulePermissionInfo).ModulePermissionID.CompareTo(CType(y, ModulePermissionInfo).ModulePermissionID)
		End Function

#End Region

	End Class
#End Region

End Namespace
