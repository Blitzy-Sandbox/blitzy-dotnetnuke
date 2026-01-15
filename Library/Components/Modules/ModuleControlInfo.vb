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
Imports System.Configuration
Imports System.Data
Imports System.Globalization
Imports System.IO
Imports System.Xml

Namespace DotNetNuke.Entities.Modules

	Public Class ModuleControlInfo

		Private _ModuleControlID As Integer
		Private _ModuleDefID As Integer
		Private _ControlKey As String
		Private _ControlTitle As String
		Private _ControlSrc As String
		Private _IconFile As String
		Private _ControlType As Integer
		Private _ViewOrder As Integer
		Private _HelpURL As String
        Private _SupportsPartialRendering As Boolean = Null.NullBoolean

		Public Sub New()
		End Sub

		Public Property ModuleControlID() As Integer
			Get
				Return _ModuleControlID
			End Get
			Set(ByVal Value As Integer)
				_ModuleControlID = Value
			End Set
		End Property
		Public Property ModuleDefID() As Integer
			Get
				Return _ModuleDefID
			End Get
			Set(ByVal Value As Integer)
				_ModuleDefID = Value
			End Set
		End Property
		Public Property ControlKey() As String
			Get
				Return _ControlKey
			End Get
			Set(ByVal Value As String)
				_ControlKey = Value
			End Set
		End Property
		Public Property ControlTitle() As String
			Get
				Return _ControlTitle
			End Get
			Set(ByVal Value As String)
				_ControlTitle = Value
			End Set
		End Property
		Public Property ControlSrc() As String
			Get
				Return _ControlSrc
			End Get
			Set(ByVal Value As String)
				_ControlSrc = Value
			End Set
		End Property
		Public Property IconFile() As String
			Get
				Return _IconFile
			End Get
			Set(ByVal Value As String)
				_IconFile = Value
			End Set
		End Property
		Public Property ControlType() As SecurityAccessLevel
			Get
				Return CType(_ControlType, SecurityAccessLevel)
			End Get
			Set(ByVal Value As SecurityAccessLevel)
				_ControlType = Value
			End Set
		End Property
		Public Property ViewOrder() As Integer
			Get
				Return _ViewOrder
			End Get
			Set(ByVal Value As Integer)
				_ViewOrder = Value
			End Set
		End Property

		Public Property HelpURL() As String
			Get
				Return _HelpURL
			End Get
			Set(ByVal Value As String)
				_HelpURL = Value
			End Set
		End Property

        Public Property SupportsPartialRendering() As Boolean
            Get
                Return _SupportsPartialRendering
            End Get
            Set(ByVal value As Boolean)
                _SupportsPartialRendering = value
            End Set
        End Property

    End Class

End Namespace

