#!/bin/bash

echo "========================================="
echo " 🧪 KORSANCIM - Unit Testler Çalıştırılıyor... "
echo "========================================="

mkdir -p build
cd build || exit 1

# CMake'e backend klasörünü kaynak gösteriyoruz
cmake ../backend
make korsancim_tests

if [ $? -eq 0 ]; then
    echo "-----------------------------------------"
    echo "🚀 Testler Başlatılıyor..."
    echo "-----------------------------------------"
    ./korsancim_tests
else
    echo "-----------------------------------------"
    echo "❌ Test Derleme Hatası!"
    echo "-----------------------------------------"
    exit 1
fi