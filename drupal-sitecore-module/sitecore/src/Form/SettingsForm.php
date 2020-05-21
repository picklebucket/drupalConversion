<?php

namespace Drupal\sitecore\Form;

use Drupal\Core\Form\ConfigFormBase;
use Drupal\Core\Form\FormStateInterface;
use Drupal\file\Entity\File;
use Drupal\node\Entity\Node;

/**
 * Class SettingsForm.
 */
class SettingsForm extends ConfigFormBase {

  /**
   * {@inheritdoc}
   */
  protected function getEditableConfigNames() {
    return [
      'sitecore.settings',
    ];
  }

  /**
   * {@inheritdoc}
   */
  public function getFormId() {
    return 'settings_form';
  }

  /**
   * {@inheritdoc}
   */
  public function buildForm(array $form, FormStateInterface $form_state) {
    $form = array(
        '#attributes' => array('enctype' => 'multipart/form-data'),
    );
    $validators = array(
        'file_validate_extensions' => array('db'),
    );
    $form['sqlite_db_file'] = [
      '#type' => 'managed_file',
      '#title' => $this->t('Sitecore Database File'),
      '#upload_validators' => $validators,
      '#upload_location' => 'public://sitecore_dbs/',
    ];
    return parent::buildForm($form, $form_state);
  }

  /**
   * {@inheritdoc}
   */
  public function submitForm(array &$form, FormStateInterface $form_state) {

    $form_file = $form_state->getValue('sqlite_db_file', 0);

    if (isset($form_file[0]) && !empty($form_file[0])) {
        $file = File::load($form_file[0]);
        $file->setPermanent();
        $file->save();
        
        $absolute_path = \Drupal::service('file_system')->realpath($file->getFileUri());
        
        try {
            
            $pdo = NULL;
            $pdo = new \PDO("sqlite:" . $absolute_path);
            
            $stmt = $pdo->query("SELECT * FROM pages");
            $rows = $stmt->fetchAll(\PDO::FETCH_ASSOC);
            
            $pdo = NULL;

            foreach($rows as $row){
                
                // TODO: process the taxonomy tree, and create vocabulary + terms
                
                // create the page
                Node::create(array(
                    'type' => 'sc_article',
                    'title' => $row['TemplateName'],
                ))
                ->save();
                
            }
            
            $this->messenger()->addStatus($this->t('Congratulations, your Sitecore Database has been imported.'));
            
        } catch (\PDOException $e) {
            $this->messenger()->addError($this->t($e->getMessage()));
        }
        
    }

  }

}
