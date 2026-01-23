"""
XiDeAI Pro - RAG Knowledge Base Setup
Bu script kod dosyalarını ChromaDB'ye indeksler.
Kullanım: python setup_rag.py
"""

import os
import sys
from pathlib import Path

# Bağımlılık kontrolü
try:
    from langchain_community.document_loaders import DirectoryLoader, TextLoader
    from langchain.text_splitter import RecursiveCharacterTextSplitter
    from langchain_community.vectorstores import Chroma
    from langchain_community.embeddings import OllamaEmbeddings
except ImportError:
    print("❌ Gerekli paketler eksik. Kurulum yapılıyor...")
    os.system("pip install langchain langchain-community chromadb ollama")
    print("✅ Kurulum tamamlandı. Lütfen script'i tekrar çalıştırın.")
    sys.exit(0)

# Yapılandırma
KNOWLEDGE_BASE_DIR = Path("./AI_Knowledge_Base")
CODEBASE_DIR = KNOWLEDGE_BASE_DIR / "codebase"
DOCS_DIR = KNOWLEDGE_BASE_DIR / "docs"
CHROMA_DIR = KNOWLEDGE_BASE_DIR / "embeddings"

# Ollama sunucu adresi (uzak sunucu için değiştirin)
OLLAMA_HOST = os.environ.get("OLLAMA_HOST", "http://localhost:11434")
EMBEDDING_MODEL = "nomic-embed-text"  # veya "all-minilm"

def main():
    print("🧠 XiDeAI Pro RAG Kurulumu Başlıyor...")
    print(f"   📁 Knowledge Base: {KNOWLEDGE_BASE_DIR.absolute()}")
    print(f"   🌐 Ollama Host: {OLLAMA_HOST}")
    print()
    
    # Klasör kontrolü
    if not CODEBASE_DIR.exists():
        print("❌ Codebase klasörü bulunamadı!")
        print("   Önce 'export_for_ai.ps1' script'ini çalıştırın.")
        sys.exit(1)
    
    # Embedding modeli kontrolü
    print(f"🔄 Embedding modeli kontrol ediliyor ({EMBEDDING_MODEL})...")
    try:
        embeddings = OllamaEmbeddings(
            model=EMBEDDING_MODEL,
            base_url=OLLAMA_HOST
        )
        # Test embedding
        embeddings.embed_query("test")
        print(f"✅ {EMBEDDING_MODEL} modeli hazır.")
    except Exception as e:
        print(f"⚠️ Embedding modeli bulunamadı. İndiriliyor...")
        os.system(f"ollama pull {EMBEDDING_MODEL}")
        embeddings = OllamaEmbeddings(model=EMBEDDING_MODEL, base_url=OLLAMA_HOST)
    
    # Dosyaları yükle
    print()
    print("📂 Kod dosyaları yükleniyor...")
    
    documents = []
    
    # C# ve Python dosyaları
    for ext in ["*.cs", "*.py"]:
        loader = DirectoryLoader(
            str(CODEBASE_DIR),
            glob=ext,
            loader_cls=TextLoader,
            loader_kwargs={"encoding": "utf-8"},
            show_progress=True
        )
        try:
            docs = loader.load()
            documents.extend(docs)
            print(f"   ✅ {len(docs)} {ext} dosyası yüklendi")
        except Exception as e:
            print(f"   ⚠️ {ext} yüklenirken hata: {e}")
    
    # Markdown dosyaları
    if DOCS_DIR.exists():
        loader = DirectoryLoader(
            str(DOCS_DIR),
            glob="*.md",
            loader_cls=TextLoader,
            loader_kwargs={"encoding": "utf-8"},
            show_progress=True
        )
        try:
            docs = loader.load()
            documents.extend(docs)
            print(f"   ✅ {len(docs)} markdown dosyası yüklendi")
        except Exception as e:
            print(f"   ⚠️ Markdown yüklenirken hata: {e}")
    
    if not documents:
        print("❌ Hiç dosya yüklenemedi!")
        sys.exit(1)
    
    print(f"\n📊 Toplam {len(documents)} dosya yüklendi.")
    
    # Chunk'lama
    print("\n✂️ Dosyalar chunk'lara ayrılıyor...")
    text_splitter = RecursiveCharacterTextSplitter(
        chunk_size=1500,
        chunk_overlap=200,
        separators=["\n\n", "\n", " ", ""]
    )
    chunks = text_splitter.split_documents(documents)
    print(f"   ✅ {len(chunks)} chunk oluşturuldu")
    
    # ChromaDB'ye indeksle
    print("\n💾 ChromaDB'ye indeksleniyor (bu biraz sürebilir)...")
    
    # Mevcut DB'yi temizle
    if CHROMA_DIR.exists():
        import shutil
        shutil.rmtree(CHROMA_DIR)
    
    vectorstore = Chroma.from_documents(
        documents=chunks,
        embedding=embeddings,
        persist_directory=str(CHROMA_DIR)
    )
    
    print(f"✅ {len(chunks)} chunk başarıyla indekslendi!")
    print(f"   📁 Veritabanı: {CHROMA_DIR.absolute()}")
    print()
    print("🎉 RAG Kurulumu Tamamlandı!")
    print("   Soru sormak için: python xideai_rag.py")

if __name__ == "__main__":
    main()
