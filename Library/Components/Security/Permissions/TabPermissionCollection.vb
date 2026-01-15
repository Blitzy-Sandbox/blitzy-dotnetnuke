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


#Region "TabPermissionCollection"
	Public Class TabPermissionCollection
		Inherits CollectionBase

		Public Sub New()
			MyBase.New()
		End Sub
		Public Sub New(ByVal TabPermissions As ArrayList)
			Dim i As Integer
			For i = 0 To TabPermissions.Count - 1
				Dim objTabPerm As Security.Permissions.TabPermissionInfo = CType(TabPermissions(i), Security.Permissions.TabPermissionInfo)
				Add(objTabPerm)
			Next
		End Sub
		Public Sub New(ByVal TabPermissions As ArrayList, ByVal TabId As Integer)
			Dim i As Integer
			For i = 0 To TabPermissions.Count - 1
				Dim objTabPerm As Security.Permissions.TabPermissionInfo = CType(TabPermissions(i), Security.Permissions.TabPermissionInfo)
				If objTabPerm.TabID = TabId Then
					Add(objTabPerm)
				End If
			Next
		End Sub
		Default Public Property Item(ByVal index As Integer) As TabPermissionInfo
			Get
				Return CType(List(index), TabPermissionInfo)
			End Get
			Set(ByVal Value As TabPermissionInfo)
				List(index) = Value
			End Set
		End Property

		Public Function Add(ByVal value As TabPermissionInfo) As Integer
			Return List.Add(value)
		End Function

		Public Function IndexOf(ByVal value As TabPermissionInfo) As Integer
			Return List.IndexOf(value)
		End Function

		Public Sub Insert(ByVal index As Integer, ByVal value As TabPermissionInfo)
			List.Insert(index, value)
		End Sub

		Public Sub Remove(ByVal value As TabPermissionInfo)
			List.Remove(value)
		End Sub

		Public Function Contains(ByVal value As TabPermissionInfo) As Boolean
			Return List.Contains(value)
		End Function

        Public Function CompareTo(ByVal objTabPermissionCollection As TabPermissionCollection) As Boolean
            If objTabPermissionCollection.Count <> Me.Count Then
                Return False
            End If
            InnerList.Sort(New CompareTabPermissions)
            objTabPermissionCollection.InnerList.Sort(New CompareTabPermissions)

            Dim objTabPermission As TabPermissionInfo
            Dim i As Integer
            For Each objTabPermission In objTabPermissionCollection
                If objTabPermissionCollection(i).TabPermissionID <> Me(i).TabPermissionID _
                Or objTabPermissionCollection(i).AllowAccess <> Me(i).AllowAccess Then
                    Return False
                End If
                i += 1
            Next
            Return True
        End Function

	End Class

	Public Class CompareTabPermissions
		Implements System.Collections.IComparer

#Region "IComparer Interface"

		Public Function Compare(ByVal x As Object, ByVal y As Object) As Integer Implements System.Collections.IComparer.Compare
			Return CType(x, TabPermissionInfo).TabPermissionID.CompareTo(CType(y, TabPermissionInfo).TabPermissionID)
		End Function

#End Region

	End Class

#End Region

End Namespace
