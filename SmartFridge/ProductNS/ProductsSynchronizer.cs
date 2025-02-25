﻿using System;
using System.Collections.Generic;
using System.Windows;

namespace SmartFridge.ProductNS
{
    internal class ProductsSynchronizer : ISynchronizer
    {
        private readonly Products m_products;        
        private readonly DBProducts m_db;
        private readonly ImageRepository m_localImgRepo;
        private readonly RemoteImageRepository m_remoteImgRepo;

        private readonly List<Product> m_deletedProducts = new List<Product>();
        private readonly List<Product> m_savedProducts = new List<Product>();
        private readonly List<Image> m_deletedImages = new List<Image>();
        private readonly List<Image> m_savedImages = new List<Image>();        

        internal ProductsSynchronizer(Products products, DBProducts db, ImageRepository localImgRepo, RemoteImageRepository remoteImgRepo)
        {
            m_db = db;
            m_localImgRepo = localImgRepo;
            m_remoteImgRepo = remoteImgRepo;

            // Wird Event emittiert, so wird das Produkt / Bild direkt in der Liste hinzugefügt
            m_products = products;
            m_products.Added   += m_savedProducts.Add;
            m_products.Changed += m_savedProducts.Add;
            m_products.Deleted += m_deletedProducts.Add;            
            m_products.SavedImg += image => m_savedImages.Add(image);
            m_products.DeletedImg += image => m_deletedImages.Add(image);
        }

        public void Synchronize()
        {
            try
            {
                OpenConnection();
                Push();
                Pull();
            }
            finally
            {
                // werden auch im Offline Fall zurückgesetzt
                //   -> Änderungen werden zurückgesetzt
                Reset();
            }
        }

        public void Reset()
        {
            m_deletedProducts.Clear();
            m_savedProducts.Clear();
            m_deletedImages.Clear();
            m_savedImages.Clear();
        }

        private void OpenConnection()
        {
            if (m_db.DbConnection.State != System.Data.ConnectionState.Open)
            {
                m_db.DbConnection.Open();
            }
        }

        private void Push()
        {
            // Jedes gelöschte / gespeicherte Produkt in der Remote Datenbank löschen / speichern
            m_deletedProducts.ForEach(m_db.Delete);
            m_savedProducts.ForEach(m_db.Save);

            m_deletedImages.ForEach(image => _ = m_remoteImgRepo.DeleteAsync(image));
            m_savedImages.ForEach(image => _ = m_remoteImgRepo.SaveAsync(image));
        }

        private void Pull()
        {
            var remoteProducts = m_db.LoadAll();
            var localProducts = new List<Product>(m_products.List);
            var deletedProducts = GetDeletedProducts(localProducts, remoteProducts);
            var changedOrCreatedProducts = GetCreatedOrChangedProducts(localProducts, remoteProducts);

            foreach (Product product in changedOrCreatedProducts) {
                // Bilder asynchron herunterladen und dann speichern
                _ = m_remoteImgRepo.LoadAsync(product.Image);
                m_remoteImgRepo.DownloadCompleted += image => m_localImgRepo.SaveAsync(image);
            }

            // Oberfläche darf nur im Main-Thread (GUI Thread) verändert werden
            Application.Current.Dispatcher.Invoke(() => {
                foreach (Product product in deletedProducts)
                    m_products.Delete(product);

                foreach (Product product in changedOrCreatedProducts)
                    m_products.AddOrEdit(product);
            });
        }

        List<Product> GetDeletedProducts(List<Product> localProducts, List<Product> remoteProducts)
        {
            List<Product> deletedProducts = new List<Product>();
            
            foreach(Product localProduct in localProducts)
            {
                // Findet Produkt aus remoteProducts, welches dieselbe ID wie localProduct hat
                // Gibt null zurück wenn kein zugehöriges Remote-Produkt gefunden wurde
                var congruentRemoteProduct = remoteProducts.Find(product => product.ID == localProduct.ID);               
                
                // remoteProduct wurde gelöscht
                if (congruentRemoteProduct == null) 
                    deletedProducts.Add(localProduct);
            }

            return deletedProducts;
        }

        List<Product> GetCreatedOrChangedProducts(List<Product> localProducts, List<Product> remoteProducts)
        {
            List<Product> changedOrCreatedProducts = new List<Product>();

            foreach(Product remoteProduct in remoteProducts)
            {
                // Findet Produkt aus localProducts, welches dieselbe ID wie remoteProduct hat
                // Gibt null zurück wenn kein zugehöriges, lokales Produkt gefunden wurde
                var congruentLocalProduct = localProducts.Find(product => product.ID == remoteProduct.ID);

                // remoteProdukt wurde neu erstellt
                // oder verändert
                if (congruentLocalProduct == null) 
                    changedOrCreatedProducts.Add(remoteProduct);
                else if (!congruentLocalProduct.ValueEqual(remoteProduct))
                    changedOrCreatedProducts.Add(remoteProduct);
            }

            return changedOrCreatedProducts;
        }
    }
}
