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

Imports ICSharpCode.SharpZipLib.Zip
Imports System.IO

Namespace DotNetNuke.Modules.Admin.ResourceInstaller

	<TypeConverter(GetType(EnumConverter))> _
	Public Enum PaFileType
		Dll
		Sql
		Ascx
		Dnn
		DataProvider
		Other
	End Enum	'PaFileType

	Public Enum PaTextEncoding
		UTF7
		UTF8
		UTF16BigEndian
		UTF16LittleEndian
		Unknown
	End Enum

	Public Class PaFile
		Inherits ResourceInstallerBase

		Private _Name As String
		Private _Path As String
		Private _Buffer() As Byte
		Private _Type As PaFileType
		Private _Encoding As PaTextEncoding

		Public Sub New(ByVal unzip As ZipInputStream, ByVal entry As ZipEntry)
            Dim s As String = entry.Name
			Dim i As Integer = s.LastIndexOf("/"c)
			If i < 0 Then
				_Name = s.Substring(0, s.Length)
				_Path = ""
			Else
				_Name = s.Substring(i + 1, s.Length - (i + 1))
				_Path = s.Substring(0, i)
			End If

            _Buffer = New [Byte](Convert.ToInt32(entry.Size) - 1) {}

            'Ignore 0 byte files during import
            If _Buffer.Length > 0 Then
                Dim size As Integer = 0
                While size < _Buffer.Length
                    size += unzip.Read(_Buffer, size, _Buffer.Length - size)
                End While
                If size <> _Buffer.Length Then
                    Throw New Exception(EXCEPTION_FileRead & _Buffer.Length & "/" & size)
                End If
                Dim ext As String = Extension

                _Encoding = PaTextEncoding.Unknown

                Select Case ext.ToLower
                    Case "dnn"
                        _Type = PaFileType.Dnn
                    Case "dll"
                        _Type = PaFileType.Dll
                    Case "ascx"
                        _Type = PaFileType.Ascx
                    Case "sql"
                        _Type = PaFileType.Sql
                        _Encoding = GetTextEncodingType(_Buffer)
                    Case Else
                        If ext.ToLower.EndsWith("dataprovider") Then
                            _Type = PaFileType.DataProvider
                            _Encoding = GetTextEncodingType(_Buffer)
                        Else
                            _Type = PaFileType.Other
                        End If
                End Select
            Else
                _Encoding = PaTextEncoding.Unknown
                _Type = PaFileType.Other
            End If

		End Sub		  'New

		Public ReadOnly Property Name() As String
			Get
				Return _Name
			End Get
		End Property

		Public Property Path() As String
			Get
				Return _Path
			End Get
			Set(ByVal Value As String)
				_Path = Value.Trim("/"c, "\"c)
			End Set
		End Property

		Public ReadOnly Property FullName() As String
			Get
				Return System.IO.Path.Combine(_Path, _Name)
			End Get
		End Property

		Public ReadOnly Property Extension() As String
			Get

				Dim ext As String = System.IO.Path.GetExtension(_Name)

				'Need to handle DataProviders and properly type them.
				'If uninstall is in the filename then it shouldn't be considered a sql file.

				'If ext.ToLower.Trim.EndsWith("dataprovider") And _Name.ToLower.IndexOf("uninstall") = -1 Then
				'    Return "sql"
				'End If

				If ext Is Nothing Or ext.Length = 0 Then
					Return ""
				Else
					Return ext.Substring(1)
				End If
			End Get
		End Property

		Private Function GetTextEncodingType(ByVal Buffer As Byte()) As PaTextEncoding
			'UTF7 = No byte higher than 127
			'UTF8 = first three bytes EF BB BF
			'UTF16BigEndian = first two bytes FE FF
			'UTF16LittleEndian = first two bytes FF FE

			'Lets do the easy ones first
			If Buffer(0) = 255 And Buffer(1) = 254 Then
				Return PaTextEncoding.UTF16LittleEndian
			End If
			If Buffer(0) = 254 And Buffer(1) = 255 Then
				Return PaTextEncoding.UTF16BigEndian
			End If
			If Buffer(0) = 239 And Buffer(1) = 187 And Buffer(2) = 191 Then
				Return PaTextEncoding.UTF8
			End If

			'This does a simple test to verify that there are no bytes with a value larger than 127
			'which would be invalid in UTF-7 encoding
			Dim i As Integer
			For i = 0 To 100
				If Buffer(i) > 127 Then
					Return PaTextEncoding.Unknown
				End If
			Next
			Return PaTextEncoding.UTF7

		End Function

		Public ReadOnly Property Encoding() As PaTextEncoding
			Get
				Return _Encoding
			End Get
		End Property

		Public ReadOnly Property Buffer() As Byte()
			Get
				Return _Buffer
			End Get
		End Property

		Public ReadOnly Property Type() As PaFileType
			Get
				Return _Type
			End Get
		End Property
	End Class
End Namespace
