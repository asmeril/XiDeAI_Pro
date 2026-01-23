"""
XiDeAI Pro - RAG Chat İstemcisi
Bu script kod tabanı hakkında sorularınıza cevap verir.
Kullanım: python xideai_rag.py
"""

import os
import sys
from pathlib import Path

# Bağımlılık kontrolü
try:
    from langchain_community.vectorstores import Chroma
    from langchain_community.embeddings import OllamaEmbeddings
    from langchain_community.llms import Ollama
    from langchain.chains import RetrievalQA
    from langchain.prompts import PromptTemplate
except ImportError:
    print("❌ Gerekli paketler eksik.")
    print("   pip install langchain langchain-community chromadb ollama")
    sys.exit(1)

# Yapılandırma
KNOWLEDGE_BASE_DIR = Path("./AI_Knowledge_Base")
CHROMA_DIR = KNOWLEDGE_BASE_DIR / "embeddings"

# Ollama sunucu adresi (uzak sunucu için değiştirin)
OLLAMA_HOST = os.environ.get("OLLAMA_HOST", "http://localhost:11434")
EMBEDDING_MODEL = "nomic-embed-text"
LLM_MODEL = "deepseek-coder-v2"  # veya "deepseek-coder:6.7b"

# Özel prompt template
PROMPT_TEMPLATE = """Sen XiDeAI Pro kod tabanı uzmanısın. 
Aşağıdaki bağlam bilgilerini kullanarak soruyu yanıtla.

BAĞLAM:
{context}

SORU: {question}

YANITLAMA KURALLARI:
1. Sadece verilen bağlama dayanarak cevap ver.
2. Kod örnekleri verirken dosya adını belirt.
3. Emin değilsen "Bu bilgi mevcut değil" de.
4. Türkçe yanıt ver.

YANIT:"""

def main():
    print("=" * 50)
    print("🧠 XiDeAI Pro - Kod Asistanı")
    print("=" * 50)
    print(f"📁 Knowledge Base: {CHROMA_DIR.absolute()}")
    print(f"🤖 Model: {LLM_MODEL}")
    print(f"🌐 Ollama: {OLLAMA_HOST}")
    print()
    
    # Veritabanı kontrolü
    if not CHROMA_DIR.exists():
        print("❌ Veritabanı bulunamadı!")
        print("   Önce 'python setup_rag.py' çalıştırın.")
        sys.exit(1)
    
    # Embedding ve LLM yükle
    print("🔄 Model yükleniyor...")
    try:
        embeddings = OllamaEmbeddings(
            model=EMBEDDING_MODEL,
            base_url=OLLAMA_HOST
        )
        
        llm = Ollama(
            model=LLM_MODEL,
            base_url=OLLAMA_HOST,
            temperature=0.3,
            num_ctx=8192  # Context window
        )
        
        vectorstore = Chroma(
            persist_directory=str(CHROMA_DIR),
            embedding_function=embeddings
        )
        
        # RetrievalQA zinciri
        prompt = PromptTemplate(
            template=PROMPT_TEMPLATE,
            input_variables=["context", "question"]
        )
        
        qa_chain = RetrievalQA.from_chain_type(
            llm=llm,
            chain_type="stuff",
            retriever=vectorstore.as_retriever(search_kwargs={"k": 5}),
            chain_type_kwargs={"prompt": prompt},
            return_source_documents=True
        )
        
        print("✅ Hazır!")
        print()
        print("💡 İpucu: 'çıkış' yazarak programı sonlandırabilirsiniz.")
        print("-" * 50)
        
    except Exception as e:
        print(f"❌ Model yüklenemedi: {e}")
        print("   Ollama'nın çalıştığından emin olun.")
        sys.exit(1)
    
    # Sohbet döngüsü
    while True:
        try:
            question = input("\n🔍 Soru: ").strip()
            
            if not question:
                continue
            
            if question.lower() in ["çıkış", "exit", "quit", "q"]:
                print("👋 Görüşmek üzere!")
                break
            
            print("\n⏳ Düşünüyorum...")
            result = qa_chain({"query": question})
            
            print("\n" + "=" * 50)
            print("📝 YANIT:")
            print("=" * 50)
            print(result["result"])
            
            # Kaynak dosyaları göster
            if result.get("source_documents"):
                print("\n📚 KAYNAKLAR:")
                sources = set()
                for doc in result["source_documents"]:
                    source = doc.metadata.get("source", "Bilinmiyor")
                    sources.add(Path(source).name)
                for src in sources:
                    print(f"   • {src}")
            
        except KeyboardInterrupt:
            print("\n\n👋 Görüşmek üzere!")
            break
        except Exception as e:
            print(f"\n❌ Hata: {e}")

if __name__ == "__main__":
    main()
