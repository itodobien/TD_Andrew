using UnityEngine;

namespace ActionGameFramework.Health
{
    /// <summary>
    /// DamageCollider is a collider-based implementation of DamageZone.
    /// It detects collisions and applies damage to the colliding object if it has a Damager component.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public class DamageCollider : DamageZone
    {
        /// <summary>
        /// Called when this collider collides with another collider.
        /// If the colliding object has a Damager component, it applies damage to the DamageableBehaviour.
        /// </summary>
        /// <param name="c">The collision data associated with this collision.</param>
        protected void OnCollisionEnter(Collision c)
        {
            // Try to get the Damager component from the colliding object
            var damager = c.gameObject.GetComponent<Damager>();
            if (damager == null)
            {
                // If no Damager component is found, exit the method
                return;
            }
            // Ensure the DamageableBehaviour is loaded
            LazyLoad();

            // Scale the damage based on the damager's damage value
            float scaledDamage = ScaleDamage(damager.damage);
            // Calculate the collision position by averaging the contact points
            Vector3 collisionPosition = ConvertContactsToPosition(c.contacts);
            // Apply the scaled damage to the DamageableBehaviour
            damageableBehaviour.TakeDamage(scaledDamage, collisionPosition, damager.alignmentProvider);

            // Notify the damager that it has successfully damaged the target
            damager.HasDamaged(collisionPosition, damageableBehaviour.configuration.alignmentProvider);
        }

        /// <summary>
        /// Converts an array of contact points to a single position by averaging the contact points.
        /// </summary>
        /// <param name="contacts">An array of contact points from the collision.</param>
        /// <returns>The average position of the contact points.</returns>
        protected Vector3 ConvertContactsToPosition(ContactPoint[] contacts)
        {
            Vector3 output = Vector3.zero;
            int length = contacts.Length;

            if (length == 0)
            {
                return output;
            }

            // Sum all contact points
            for (int i = 0; i < length; i++)
            {
                output += contacts[i].point;
            }

            // Calculate the average position
            output = output / length;
            return output;
        }
    }
}