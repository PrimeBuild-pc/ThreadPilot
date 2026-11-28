"""Remove Inno Setup's space padding from the two PE strings SignPath checks."""

import argparse
import ctypes
from ctypes import wintypes
import mmap
from pathlib import Path
import struct


def find_resource_section(image):
    pe_offset, = struct.unpack_from("<I", image, 0x3C)
    if image[:2] != b"MZ" or image[pe_offset:pe_offset + 4] != b"PE\0\0":
        raise ValueError("Expected a Windows PE installer.")

    coff_header = pe_offset + 4
    section_count, = struct.unpack_from("<H", image, coff_header + 2)
    optional_size, = struct.unpack_from("<H", image, coff_header + 16)
    optional_header = coff_header + 20
    magic, = struct.unpack_from("<H", image, optional_header)
    directory_offset = {0x10B: 96, 0x20B: 112}.get(magic)
    if directory_offset is None or optional_size < directory_offset + 40:
        raise ValueError("Installer has no supported PE optional header.")

    data_directories = optional_header + directory_offset
    certificate_offset, certificate_size = struct.unpack_from("<II", image, data_directories + 4 * 8)
    if certificate_offset or certificate_size:
        raise ValueError("Refusing to change metadata on a signed installer.")

    resource_rva, resource_size = struct.unpack_from("<II", image, data_directories + 2 * 8)
    sections = optional_header + optional_size
    for index in range(section_count):
        section = sections + index * 40
        section_rva, raw_size, raw_offset = struct.unpack_from("<III", image, section + 12)
        delta = resource_rva - section_rva
        if resource_size and 0 <= delta and delta + resource_size <= raw_size:
            start = raw_offset + delta
            end = start + resource_size
            if end <= len(image):
                return start, end, optional_header + 64

    raise ValueError("Installer PE resources were not found.")


def find_string_edit(image, resource_start, resource_end, name, expected):
    key = (name + "\0").encode("utf-16-le")
    key_offset = image.find(key, resource_start, resource_end)
    if key_offset < resource_start + 6 or image.find(key, key_offset + len(key), resource_end) != -1:
        raise ValueError(f"Expected one {name} entry in the installer resources.")

    header = key_offset - 6
    entry_size, value_length, value_type = struct.unpack_from("<HHH", image, header)
    value_offset = (key_offset + len(key) + 3) & ~3
    value_end = value_offset + value_length * 2
    if value_type != 1 or not value_length or not value_end <= header + entry_size <= resource_end:
        raise ValueError(f"Malformed {name} version-resource entry.")

    value = image[value_offset:value_end].decode("utf-16-le").rstrip("\0")
    if value.rstrip(" ") != expected:
        raise ValueError(f"Unexpected {name} value: {value!r}")

    # End the string at its real length and zero the unused fixed-width storage.
    encoded = (expected + "\0").encode("utf-16-le")
    value_bytes = encoded.ljust(value_end - value_offset, b"\0")
    return header + 2, len(encoded) // 2, value_offset, value_bytes


def update_checksum(image, offset):
    checksum_mapped_file = ctypes.WinDLL("imagehlp").CheckSumMappedFile
    checksum_mapped_file.argtypes = (
        ctypes.c_void_p, wintypes.DWORD, ctypes.POINTER(wintypes.DWORD), ctypes.POINTER(wintypes.DWORD)
    )
    checksum_mapped_file.restype = ctypes.c_void_p
    old_checksum = wintypes.DWORD()
    new_checksum = wintypes.DWORD()
    buffer_start = ctypes.c_char.from_buffer(image)
    if not checksum_mapped_file(ctypes.addressof(buffer_start), len(image), old_checksum, new_checksum):
        raise OSError("Windows could not calculate the installer PE checksum.")
    struct.pack_into("<I", image, offset, new_checksum.value)


def normalize(path, version):
    with path.open("r+b") as installer, mmap.mmap(installer.fileno(), 0) as image:
        start, end, checksum_offset = find_resource_section(image)
        edits = [
            find_string_edit(image, start, end, "ProductName", "ThreadPilot"),
            find_string_edit(image, start, end, "ProductVersion", version),
        ]
        for length_offset, length, value_offset, value in edits:
            struct.pack_into("<H", image, length_offset, length)
            image[value_offset:value_offset + len(value)] = value
        update_checksum(image, checksum_offset)
        image.flush()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("installer", type=Path)
    parser.add_argument("version")
    args = parser.parse_args()
    normalize(args.installer, args.version)
