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

Namespace DotNetNuke.Data

	'
	' SQLAttributes
	'
	' Sample Usage:
	'
	' Public Function SomeMethod( _
	'     ByVal SomeParameter1 As String,
	'     <SqlParameter(, , , , , ParameterDirection.Output)> ByVal SomeParameter2 As Integer) As Integer
	'
	'

	<AttributeUsage(AttributeTargets.Parameter)> _
	Public Class SqlParameterAttribute
		Inherits Attribute
		Private _name As String
		Private _paramTypeDefined As Boolean
		Private _paramType As SqlDbType
		Private _size As Integer
		Private _precision As Byte
		Private _scale As Byte
		Private _directionDefined As Boolean
		Private _direction As ParameterDirection

		Public Sub New(Optional ByVal name As String = Nothing, Optional ByVal paramType As SqlDbType = Nothing, Optional ByVal size As Integer = Nothing, Optional ByVal precision As Byte = Nothing, Optional ByVal scale As Byte = Nothing, Optional ByVal direction As ParameterDirection = Nothing)
			MyBase.New()
			_name = name
			_paramType = paramType
			If paramType = Nothing Then
				_paramTypeDefined = False
			Else
				_paramTypeDefined = True
			End If
			_size = size
			_precision = precision
			_scale = scale
			_direction = direction
			If direction = Nothing Then
				_directionDefined = False
			Else
				_directionDefined = True
			End If
		End Sub	   'New

		Public Property Name() As String
			Get
				If _name Is Nothing Then
					Return String.Empty
				Else
					Return _name
				End If
			End Get

			Set(ByVal Value As String)
				_name = Value
			End Set
		End Property

		Public Property Size() As Integer
			Get
				Return _size
			End Get

			Set(ByVal Value As Integer)
				_size = Value
			End Set
		End Property

		Public Property Precision() As Byte
			Get
				Return _precision
			End Get

			Set(ByVal Value As Byte)
				_precision = Value
			End Set
		End Property

		Public Property Scale() As Byte
			Get
				Return _scale
			End Get

			Set(ByVal Value As Byte)
				_scale = Value
			End Set
		End Property

		Public Property Direction() As ParameterDirection
			Get
				Return _direction
			End Get

			Set(ByVal Value As ParameterDirection)
				_direction = Value
			End Set
		End Property

		Public Property SqlDbType() As SqlDbType
			Get
				Return _paramType
			End Get

			Set(ByVal Value As SqlDbType)
				_paramType = Value
			End Set
		End Property

		Public ReadOnly Property IsNameDefined() As Boolean
			Get
				IsNameDefined = True
				If _name Is Nothing Then
					IsNameDefined = False
				Else
					If _name.Length = 0 Then
						IsNameDefined = False
					End If
				End If
				Return IsNameDefined
			End Get
		End Property

		Public ReadOnly Property IsSizeDefined() As Boolean
			Get
				Return _size <> 0
			End Get
		End Property

		Public ReadOnly Property IsTypeDefined() As Boolean
			Get
				Return _paramTypeDefined
			End Get
		End Property

		Public ReadOnly Property IsDirectionDefined() As Boolean
			Get
				Return _directionDefined
			End Get
		End Property

		Public ReadOnly Property IsScaleDefined() As Boolean
			Get
				Return _scale <> 0
			End Get
		End Property

		Public ReadOnly Property IsPrecisionDefined() As Boolean
			Get
				Return _precision <> 0
			End Get
		End Property

	End Class

End Namespace

Namespace DotNetNuke.Data
	<AttributeUsage(AttributeTargets.Parameter)> _
	Public NotInheritable Class NonCommandParameterAttribute
		Inherits Attribute
	End Class

End Namespace
