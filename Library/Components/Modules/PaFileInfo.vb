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

Imports System.Xml.Serialization

Namespace DotNetNuke.Entities.Modules

    Public Class PaFileInfo
        Private _Name As String
        Private _Path As String
        Private _FullPath As String
        Private _Buffer As Byte()

        Public Sub New()
            MyBase.new()
        End Sub

        Public Sub New(ByVal Name As String, ByVal Path As String, ByVal FullPath As String)
            MyBase.new()

            _Name = Name
            _Path = Path
            _FullPath = FullPath

        End Sub

        Public Property Buffer() As Byte()
            Get
                Return _Buffer
            End Get
            Set(ByVal Value As Byte())
                _Buffer = Value
            End Set
        End Property

        Public ReadOnly Property FullName() As String
            Get
                Return _FullPath & "\" & _Name
            End Get
        End Property

        Public Property Name() As String
            Get
                Return _Name
            End Get
            Set(ByVal Value As String)
                _Name = Value
            End Set
        End Property

        Public Property FullPath() As String
            Get
                Return _FullPath
            End Get
            Set(ByVal Value As String)
                _FullPath = Value
            End Set
        End Property

        Public Property Path() As String
            Get
                Return _Path
            End Get
            Set(ByVal Value As String)
                _Path = Value
            End Set
        End Property

    End Class
End Namespace
