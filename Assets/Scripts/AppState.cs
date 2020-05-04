﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Virgis {

    // AppState is a global singleton object that stores
    // app states, such as EditSession, etc.
    //
    // Singleton pattern taken from https://learn.unity.com/tutorial/level-generation
    public class AppState : MonoBehaviour {
        public static AppState instance = null;

        private EditSession _editSession;

        void Awake() {
            print("AppState awakens");
            if (instance == null) {
                print("AppState instance assigned");
                instance = this;
            } else if (instance != this) {
                // there cannot be another instance
                print("AppState found another instance");
                Destroy(gameObject);
            }

            DontDestroyOnLoad(gameObject);
            InitApp();
        }

        public EditSession editSession {
            get => _editSession;
        }

        public bool InEditSession() {
            return _editSession.IsActive();
        }

        public void StartEditSession() {
            _editSession.Start();
        }

        public void StopSaveEditSession() {
            _editSession.StopAndSave();
        }

        public void StopDiscardEditSession() {
            _editSession.StopAndDiscard();
        }

        public void AddStartEditSessionListener(UnityAction action) {
            _editSession.AddStartEditSessionListener(action);
        }

        public void AddEndEditSessionListener(UnityAction action) {
            _editSession.AddEndEditSessionListener(action);
        }

        private void InitApp() {
            _editSession = new EditSession();
        }

    }
}